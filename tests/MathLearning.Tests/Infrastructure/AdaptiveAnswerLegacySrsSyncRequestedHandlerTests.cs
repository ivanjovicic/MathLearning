using MathLearning.Domain.Entities;
using MathLearning.Domain.Events;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.EventBus.Handlers;
using MathLearning.Infrastructure.Services.Idempotency;
using MathLearning.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Infrastructure;

public sealed class AdaptiveAnswerLegacySrsSyncRequestedHandlerTests
{
    [Fact]
    public async Task Handle_DuplicateReplay_UpdatesLegacySrsOnce()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        await using (var seedDb = database.CreateContext())
        {
            await seedDb.Database.EnsureCreatedAsync();
            await TestDbContextFactory.SeedAsync(seedDb);
        }

        var ev = new AdaptiveAnswerLegacySrsSyncRequested(
            "1",
            1,
            true,
            5,
            Guid.NewGuid(),
            Guid.NewGuid());

        await using (var firstDb = database.CreateContext())
        {
            var handler = CreateHandler(firstDb);
            await handler.Handle(ev, CancellationToken.None);
        }

        await using (var secondDb = database.CreateContext())
        {
            var handler = CreateHandler(secondDb);
            await handler.Handle(ev, CancellationToken.None);
        }

        await using var verification = database.CreateContext();
        var stat = await verification.QuestionStats.SingleAsync(x => x.UserId == "1" && x.QuestionId == 1);
        Assert.Equal(1, stat.SuccessStreak);
        Assert.Equal(1.35, stat.Ease, 2);
        Assert.Equal(1, await verification.IdempotencyLedgers.CountAsync(x =>
            x.UserId == "1" &&
            x.OperationType == QuizOperationTypes.SrsUpdate &&
            x.Status == IdempotencyLedgerStatuses.Completed));
    }

    [Fact]
    public async Task Handle_SameOperationDifferentPayload_ThrowsConflict()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        await using (var seedDb = database.CreateContext())
        {
            await seedDb.Database.EnsureCreatedAsync();
            await TestDbContextFactory.SeedAsync(seedDb);
        }

        var ev = new AdaptiveAnswerLegacySrsSyncRequested(
            "1",
            1,
            true,
            5,
            Guid.NewGuid(),
            Guid.NewGuid());

        await using (var firstDb = database.CreateContext())
        {
            var handler = CreateHandler(firstDb);
            await handler.Handle(ev, CancellationToken.None);
        }

        var conflicting = ev with { IsCorrect = false };
        await using var secondDb = database.CreateContext();
        var replayHandler = CreateHandler(secondDb);
        await Assert.ThrowsAsync<IdempotencyLedgerConflictException>(() =>
            replayHandler.Handle(conflicting, CancellationToken.None));

        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.QuestionStats.CountAsync(x => x.UserId == "1" && x.QuestionId == 1));
        Assert.Equal(1, await verification.IdempotencyLedgers.CountAsync(x =>
            x.UserId == "1" &&
            x.OperationType == QuizOperationTypes.SrsUpdate));
    }

    private static AdaptiveAnswerLegacySrsSyncRequestedHandler CreateHandler(ApiDbContext db)
    {
        var observability = new IdempotencyObservabilityService(NullLogger<IdempotencyObservabilityService>.Instance);
        var ledgerService = new IdempotencyLedgerService(
            db,
            NullLogger<IdempotencyLedgerService>.Instance,
            observability);

        var srsService = new SrsService(db);
        return new AdaptiveAnswerLegacySrsSyncRequestedHandler(
            db,
            srsService,
            ledgerService,
            NullLogger<AdaptiveAnswerLegacySrsSyncRequestedHandler>.Instance);
    }

    private sealed class SqliteFileTestDatabase : IAsyncDisposable
    {
        private readonly string filePath;
        private readonly string connectionString;

        private SqliteFileTestDatabase(string filePath, string connectionString)
        {
            this.filePath = filePath;
            this.connectionString = connectionString;
        }

        public static Task<SqliteFileTestDatabase> CreateAsync()
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"mathlearning-adaptive-srs-sync-{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = filePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = false,
                DefaultTimeout = 30
            }.ToString();

            return Task.FromResult(new SqliteFileTestDatabase(filePath, connectionString));
        }

        public ApiDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApiDbContext>()
                .UseSqlite(connectionString)
                .Options;

            return new ApiDbContext(options);
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
            }

            return ValueTask.CompletedTask;
        }
    }
}
