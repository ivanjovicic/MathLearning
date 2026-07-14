using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Persistance.Models;
using MathLearning.Infrastructure.Services.EventBus;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MathLearning.Tests.Database;

public sealed class OutboxBatchProcessorTests
{
    [Fact]
    public void SanitizePersistedError_RedactsSecretsAndTruncates()
    {
        var error = new InvalidOperationException(
            "password=supersecret token=abc123 first line\r\nsecond line with extra content that should be truncated");

        var persisted = OutboxBatchProcessor.SanitizePersistedError(error, 48);

        Assert.DoesNotContain("supersecret", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", persisted, StringComparison.Ordinal);
        Assert.Contains("password=<redacted>", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", persisted, StringComparison.Ordinal);
        Assert.True(persisted.Length <= 48, $"Expected <= 48 chars but got {persisted.Length}: {persisted}");
    }

    [Fact]
    [Trait("Category", "PostgresProvider")]
    public async Task ConcurrentProcessors_ClaimDistinctRowsWithoutDuplicatePublish()
    {
        if (!IsValidationRequired())
        {
            return;
        }

        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();
        await SeedOutboxAsync(database, count: 6);

        var bus = new RecordingEventBus(delayPerPublish: TimeSpan.FromMilliseconds(100));
        var options = new OutboxProcessingOptions { BatchSize = 3 };

        await using var firstDb = new AppDbContext(database.CreateAppOptions());
        await using var secondDb = new AppDbContext(database.CreateAppOptions());

        var first = new OutboxBatchProcessor(firstDb, bus, NullLogger<OutboxBatchProcessor>.Instance, options: options);
        var second = new OutboxBatchProcessor(secondDb, bus, NullLogger<OutboxBatchProcessor>.Instance, options: options);

        var firstTask = first.ProcessBatchAsync(CancellationToken.None);
        await Task.Delay(20);
        var secondTask = second.ProcessBatchAsync(CancellationToken.None);

        var processed = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(6, processed.Sum());
        Assert.All(processed, count => Assert.True(count > 0, $"Expected both workers to claim rows but got counts: {string.Join(", ", processed)}"));

        await using var verification = new AppDbContext(database.CreateAppOptions());
        var rows = await verification.Outbox.OrderBy(x => x.OccurredUtc).ToListAsync();
        Assert.Equal(6, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.NotNull(row.ProcessedUtc);
            Assert.Equal(0, row.Attempts);
            Assert.Null(row.LastError);
            Assert.Null(row.DeadLetteredUtc);
            Assert.Null(row.NextAttemptUtc);
        });

        Assert.Equal(6, bus.PublishCounts.Count);
        Assert.All(bus.PublishCounts.Values, count => Assert.Equal(1, count));
    }

    [Fact]
    [Trait("Category", "PostgresProvider")]
    public async Task FailedMessages_BackOffThenDeadLetter_WithBoundedRedactedError()
    {
        if (!IsValidationRequired())
        {
            return;
        }

        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();

        var messageId = await SeedSingleOutboxAsync(database);
        var bus = new ThrowingEventBus("password=supersecret token=abc123 this message should be redacted and trimmed before persistence");
        var options = new OutboxProcessingOptions
        {
            BatchSize = 1,
            MaxAttempts = 3,
            MaxPersistedErrorLength = 48,
            InitialRetryDelay = TimeSpan.FromSeconds(1),
            MaxRetryDelay = TimeSpan.FromSeconds(5)
        };

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await using var db = new AppDbContext(database.CreateAppOptions());
            var processor = new OutboxBatchProcessor(db, bus, NullLogger<OutboxBatchProcessor>.Instance, options: options);

            var processedCount = await processor.ProcessBatchAsync(CancellationToken.None);
            Assert.Equal(1, processedCount);

            await using var verification = new AppDbContext(database.CreateAppOptions());
            var row = await verification.Outbox.SingleAsync(x => x.Id == messageId);

            Assert.Equal(attempt, row.Attempts);
            Assert.DoesNotContain("supersecret", row.LastError, StringComparison.Ordinal);
            Assert.DoesNotContain("abc123", row.LastError, StringComparison.Ordinal);
            Assert.Contains("<redacted>", row.LastError, StringComparison.Ordinal);
            Assert.True(row.LastError!.Length <= 48, $"Expected <= 48 chars but got {row.LastError.Length}: {row.LastError}");

            if (attempt < 3)
            {
                Assert.Null(row.DeadLetteredUtc);
                Assert.NotNull(row.NextAttemptUtc);
                Assert.True(row.NextAttemptUtc > DateTime.UtcNow.AddMilliseconds(-500));

                row.NextAttemptUtc = DateTime.UtcNow.AddSeconds(-1);
                await verification.SaveChangesAsync();
            }
            else
            {
                Assert.NotNull(row.DeadLetteredUtc);
                Assert.Null(row.NextAttemptUtc);
                Assert.Null(row.ProcessedUtc);
            }
        }
    }

    [Fact]
    [Trait("Category", "PostgresProvider")]
    public async Task CancelledBatch_RollsBackClaimAndAllowsLaterRecovery()
    {
        if (!IsValidationRequired())
        {
            return;
        }

        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();

        var messageId = await SeedSingleOutboxAsync(database);
        var blockingBus = new BlockingCancellationEventBus();

        await using (var db = new AppDbContext(database.CreateAppOptions()))
        {
            var processor = new OutboxBatchProcessor(db, blockingBus, NullLogger<OutboxBatchProcessor>.Instance);
            using var cancellation = new CancellationTokenSource();
            var processingTask = processor.ProcessBatchAsync(cancellation.Token);

            await blockingBus.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processingTask);
        }

        await using (var verification = new AppDbContext(database.CreateAppOptions()))
        {
            var row = await verification.Outbox.SingleAsync(x => x.Id == messageId);
            Assert.Equal(0, row.Attempts);
            Assert.Null(row.ProcessedUtc);
            Assert.Null(row.DeadLetteredUtc);
        }

        var successBus = new RecordingEventBus();
        await using (var db = new AppDbContext(database.CreateAppOptions()))
        {
            var recoveryProcessor = new OutboxBatchProcessor(db, successBus, NullLogger<OutboxBatchProcessor>.Instance);
            var processed = await recoveryProcessor.ProcessBatchAsync(CancellationToken.None);
            Assert.Equal(1, processed);
        }

        await using var finalVerification = new AppDbContext(database.CreateAppOptions());
        var recovered = await finalVerification.Outbox.SingleAsync(x => x.Id == messageId);
        Assert.NotNull(recovered.ProcessedUtc);
        Assert.Equal(1, successBus.PublishCounts[messageId]);
    }

    [Fact]
    [Trait("Category", "PostgresProvider")]
    public async Task MissingOutboxTable_SurfaceUndefinedTableForWorkerGuard()
    {
        if (!IsValidationRequired())
        {
            return;
        }

        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = new AppDbContext(database.CreateAppOptions());
        var processor = new OutboxBatchProcessor(db, new RecordingEventBus(), NullLogger<OutboxBatchProcessor>.Instance);

        var error = await Assert.ThrowsAsync<PostgresException>(() => processor.ProcessBatchAsync(CancellationToken.None));

        Assert.Equal(PostgresErrorCodes.UndefinedTable, error.SqlState);
    }

    private static bool IsValidationRequired()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("POSTGRES_PROVIDER_TESTS_REQUIRED"),
            "1",
            StringComparison.Ordinal);
    }

    private static async Task SeedOutboxAsync(PostgresTestDatabase database, int count)
    {
        await using var db = new AppDbContext(database.CreateAppOptions());
        var now = DateTime.UtcNow;

        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            db.Outbox.Add(new OutboxMessage
            {
                Id = id,
                OccurredUtc = now.AddMilliseconds(i),
                Type = "test.event",
                PayloadJson = $"{{\"id\":\"{id}\"}}",
                Attempts = 0
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedSingleOutboxAsync(PostgresTestDatabase database)
    {
        await using var db = new AppDbContext(database.CreateAppOptions());
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredUtc = DateTime.UtcNow,
            Type = "test.event",
            PayloadJson = null!,
            Attempts = 0
        };
        message.PayloadJson = $"{{\"id\":\"{message.Id}\"}}";

        db.Outbox.Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }

    private sealed class RecordingEventBus : IEventBus
    {
        private readonly TimeSpan delayPerPublish;

        public RecordingEventBus(TimeSpan? delayPerPublish = null)
        {
            this.delayPerPublish = delayPerPublish ?? TimeSpan.Zero;
        }

        public Dictionary<Guid, int> PublishCounts { get; } = new();

        public async Task PublishAsync(string type, string payloadJson, CancellationToken ct)
        {
            var id = ExtractId(payloadJson);
            lock (PublishCounts)
            {
                PublishCounts.TryGetValue(id, out var count);
                PublishCounts[id] = count + 1;
            }

            if (delayPerPublish > TimeSpan.Zero)
            {
                await Task.Delay(delayPerPublish, ct);
            }
        }
    }

    private sealed class ThrowingEventBus : IEventBus
    {
        private readonly string message;

        public ThrowingEventBus(string message)
        {
            this.message = message;
        }

        public Task PublishAsync(string type, string payloadJson, CancellationToken ct)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class BlockingCancellationEventBus : IEventBus
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task PublishAsync(string type, string payloadJson, CancellationToken ct)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
    }

    private static Guid ExtractId(string payloadJson)
    {
        var marker = "\"id\":\"";
        var start = payloadJson.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return Guid.Parse(payloadJson.Trim('{', '}', '"'));
        }

        start += marker.Length;
        var end = payloadJson.IndexOf('"', start);
        var value = payloadJson[start..end];

        return Guid.TryParse(value, out var guid)
            ? guid
            : GuidUtility(value);
    }

    private static Guid GuidUtility(string value)
    {
        Span<byte> bytes = stackalloc byte[16];
        var source = System.Text.Encoding.UTF8.GetBytes(value);
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = i < source.Length ? source[i] : (byte)i;
        }

        return new Guid(bytes);
    }
}
