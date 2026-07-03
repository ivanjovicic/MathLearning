using MathLearning.Api.Endpoints;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Endpoints;

public sealed class ApiDbTransactionHelpersRelationalTests
{
    [Fact]
    public async Task SuccessfulAction_CommitsDurableStateAndReturnsResult()
    {
        await using var database = await TransactionTestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        var result = await ApiDbTransactionHelpers.ExecuteWithSerializableRetryAsync(
            db,
            NullLogger.Instance,
            async () =>
            {
                db.ApplicationLogs.Add(NewLog("committed"));
                await Task.Yield();
                return 42;
            },
            CancellationToken.None);

        Assert.Equal(42, result);

        await using var verification = database.CreateContext();
        var persisted = await verification.ApplicationLogs.SingleAsync();
        Assert.Equal("committed", persisted.Message);
    }

    [Fact]
    public async Task ActionThrowsAfterInnerSave_RollsBackAlreadyIssuedSql()
    {
        await using var database = await TransactionTestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ApiDbTransactionHelpers.ExecuteWithSerializableRetryAsync<int>(
                db,
                NullLogger.Instance,
                async () =>
                {
                    db.ApplicationLogs.Add(NewLog("must-rollback"));
                    await db.SaveChangesAsync();
                    throw new InvalidOperationException("transaction-body-failed");
                },
                CancellationToken.None));

        Assert.Equal("transaction-body-failed", error.Message);

        await using var verification = database.CreateContext();
        Assert.False(await verification.ApplicationLogs.AnyAsync());
    }

    [Fact]
    public async Task FirstConcurrencyConflict_RetriesFromCleanTrackerAndPersistsOnce()
    {
        await using var database = await TransactionTestDatabase.CreateAsync();
        await using var db = database.CreateFailingContext(concurrencyFailures: 1);
        var actionCalls = 0;

        var result = await ApiDbTransactionHelpers.ExecuteWithSerializableRetryAsync(
            db,
            NullLogger.Instance,
            () =>
            {
                var attempt = Interlocked.Increment(ref actionCalls);
                db.ApplicationLogs.Add(NewLog($"attempt-{attempt}"));
                return Task.FromResult(attempt);
            },
            CancellationToken.None);

        Assert.Equal(2, result);
        Assert.Equal(2, actionCalls);
        Assert.Equal(2, db.SaveAttempts);

        await using var verification = database.CreateContext();
        var persisted = await verification.ApplicationLogs.SingleAsync();
        Assert.Equal("attempt-2", persisted.Message);
    }

    [Fact]
    public async Task RepeatedConcurrencyConflicts_StopAfterThreeAttemptsWithoutPartialRows()
    {
        await using var database = await TransactionTestDatabase.CreateAsync();
        await using var db = database.CreateFailingContext(concurrencyFailures: 3);
        var actionCalls = 0;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            ApiDbTransactionHelpers.ExecuteWithSerializableRetryAsync(
                db,
                NullLogger.Instance,
                () =>
                {
                    var attempt = Interlocked.Increment(ref actionCalls);
                    db.ApplicationLogs.Add(NewLog($"never-commit-{attempt}"));
                    return Task.FromResult(attempt);
                },
                CancellationToken.None));

        Assert.Equal(3, actionCalls);
        Assert.Equal(3, db.SaveAttempts);

        await using var verification = database.CreateContext();
        Assert.False(await verification.ApplicationLogs.AnyAsync());
    }

    [Fact]
    public async Task CancelledOperation_DoesNotCommitState()
    {
        await using var database = await TransactionTestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ApiDbTransactionHelpers.ExecuteWithSerializableRetryAsync(
                db,
                NullLogger.Instance,
                () =>
                {
                    db.ApplicationLogs.Add(NewLog("cancelled"));
                    return Task.FromResult(true);
                },
                cancellation.Token));

        await using var verification = database.CreateContext();
        Assert.False(await verification.ApplicationLogs.AnyAsync());
    }

    private static ApplicationLog NewLog(string message) => new()
    {
        Timestamp = DateTime.UtcNow,
        Level = "Information",
        Message = message
    };

    private sealed class ConcurrencyFailingApiDbContext : ApiDbContext
    {
        private int remainingFailures;

        public ConcurrencyFailingApiDbContext(
            DbContextOptions<ApiDbContext> options,
            int concurrencyFailures)
            : base(options)
        {
            remainingFailures = concurrencyFailures;
        }

        public int SaveAttempts { get; private set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveAttempts++;
            if (Interlocked.Decrement(ref remainingFailures) >= 0)
                throw new DbUpdateConcurrencyException("Injected transaction retry conflict.");

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class TransactionTestDatabase : IAsyncDisposable
    {
        private readonly string path;
        private readonly DbContextOptions<ApiDbContext> options;

        private TransactionTestDatabase(string path, DbContextOptions<ApiDbContext> options)
        {
            this.path = path;
            this.options = options;
        }

        public static async Task<TransactionTestDatabase> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"mathlearning-transaction-helper-{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = false,
                DefaultTimeout = 30
            }.ToString();

            var options = new DbContextOptionsBuilder<ApiDbContext>()
                .UseSqlite(connectionString)
                .Options;

            var database = new TransactionTestDatabase(path, options);
            await using var setup = database.CreateContext();
            await setup.Database.EnsureCreatedAsync();
            await setup.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=30000;");
            return database;
        }

        public ApiDbContext CreateContext() => new(options);

        public ConcurrencyFailingApiDbContext CreateFailingContext(int concurrencyFailures) =>
            new(options, concurrencyFailures);

        public ValueTask DisposeAsync()
        {
            DeleteIfExists(path);
            DeleteIfExists($"{path}-wal");
            DeleteIfExists($"{path}-shm");
            return ValueTask.CompletedTask;
        }

        private static void DeleteIfExists(string file)
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }
}
