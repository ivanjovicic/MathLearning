using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Idempotency;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Idempotency;

public sealed class RelationalIdempotencyTransactionTests
{
    [Fact]
    public async Task SharedLedger_Rollback_RemovesLedgerCompletionAndDomainMutation()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        const string userId = "shared-rollback-user";
        await database.SeedUserAsync(userId, coins: 100);

        await using (var db = database.CreateContext())
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var service = CreateSharedService(db);
            var begin = await service.BeginOrGetExistingAsync(
                userId,
                QuizOperationTypes.QuizAnswer,
                "shared-rollback-operation",
                "shared-rollback-key",
                "/api/quiz/answer",
                new { questionId = 1, answer = "2" });

            var profile = await db.UserProfiles.SingleAsync(x => x.UserId == userId);
            profile.Coins = 75;
            await db.SaveChangesAsync();

            await service.CompleteAsync(
                begin.LedgerId,
                new { success = true, coins = profile.Coins },
                httpStatus: 200);

            Assert.Equal(1, await db.IdempotencyLedgers.CountAsync());
            Assert.Equal(
                IdempotencyLedgerStatuses.Completed,
                (await db.IdempotencyLedgers.SingleAsync()).Status);

            await transaction.RollbackAsync();
        }

        await using var verification = database.CreateContext();
        Assert.False(await verification.IdempotencyLedgers.AnyAsync());
        Assert.Equal(100, await verification.UserProfiles
            .Where(x => x.UserId == userId)
            .Select(x => x.Coins)
            .SingleAsync());
    }

    [Fact]
    public async Task EconomyTransaction_Rollback_RemovesCompletionAndBalanceMutation()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        const string userId = "economy-rollback-user";
        await database.SeedUserAsync(userId, coins: 100);

        await using (var db = database.CreateContext())
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var service = CreateEconomyService(db);
            var begin = await service.BeginOrGetExistingAsync(
                userId,
                "coins_spend",
                "economy-rollback-key",
                new { amount = 25, reason = "hint" },
                operationId: "economy-rollback-operation");

            var profile = await db.UserProfiles.SingleAsync(x => x.UserId == userId);
            profile.Coins -= 25;
            await db.SaveChangesAsync();

            await service.CompleteAsync(
                begin.TransactionId,
                new { success = true, balance = profile.Coins });

            Assert.Equal(1, await db.EconomyTransactions.CountAsync());
            Assert.Equal(
                EconomyTransactionStatus.Completed,
                (await db.EconomyTransactions.SingleAsync()).Status);

            await transaction.RollbackAsync();
        }

        await using var verification = database.CreateContext();
        Assert.False(await verification.EconomyTransactions.AnyAsync());
        Assert.Equal(100, await verification.UserProfiles
            .Where(x => x.UserId == userId)
            .Select(x => x.Coins)
            .SingleAsync());
    }

    [Fact]
    public async Task SharedLedger_ConcurrentDuplicateInsert_ReusesSinglePendingLedger_ThenReplaysCompletion()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        var coordinator = new OrderedInsertCoordinator();

        var firstTask = BeginSharedAsync(database, coordinator, participant: 1);
        var secondTask = BeginSharedAsync(database, coordinator, participant: 2);
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(1, results.Count(x => x.ShouldProcess));
        Assert.Equal(1, results.Count(x => x.IsExisting));
        Assert.Single(results.Select(x => x.LedgerId).Distinct());

        await using (var completionDb = database.CreateContext())
        {
            var service = CreateSharedService(completionDb);
            await service.CompleteAsync(
                results[0].LedgerId,
                new { success = true, score = 10 },
                httpStatus: 201);
        }

        await using var replayDb = database.CreateContext();
        var replayService = CreateSharedService(replayDb);
        var replay = await replayService.BeginOrGetExistingAsync(
            "shared-race-user",
            QuizOperationTypes.QuizAnswer,
            "shared-race-operation",
            "shared-race-key",
            "/api/quiz/answer",
            new { questionId = 1, answer = "2" });

        Assert.True(replay.IsExisting);
        Assert.True(replay.IsCompleted);
        Assert.False(replay.ShouldProcess);
        Assert.Equal(201, replay.Ledger.HttpStatus);
        Assert.Equal(1, await replayDb.IdempotencyLedgers.CountAsync());
    }

    [Fact]
    public async Task EconomyTransaction_ConcurrentDuplicateInsert_ReusesSinglePendingTransaction_ThenReplaysCompletion()
    {
        await using var database = await SqliteFileTestDatabase.CreateAsync();
        var coordinator = new OrderedInsertCoordinator();

        var firstTask = BeginEconomyAsync(database, coordinator, participant: 1);
        var secondTask = BeginEconomyAsync(database, coordinator, participant: 2);
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(1, results.Count(x => x.ShouldProcess));
        Assert.Equal(1, results.Count(x => x.IsExisting));
        Assert.Single(results.Select(x => x.TransactionId).Distinct());

        await using (var completionDb = database.CreateContext())
        {
            var service = CreateEconomyService(completionDb);
            await service.CompleteAsync(
                results[0].TransactionId,
                new { success = true, balance = 90 });
        }

        await using var replayDb = database.CreateContext();
        var replayService = CreateEconomyService(replayDb);
        var replay = await replayService.BeginOrGetExistingAsync(
            "economy-race-user",
            "coins_spend",
            "economy-race-key",
            new { amount = 10, reason = "hint" },
            operationId: "economy-race-operation");

        Assert.True(replay.IsExisting);
        Assert.True(replay.IsCompleted);
        Assert.False(replay.ShouldProcess);
        Assert.Equal(1, await replayDb.EconomyTransactions.CountAsync());
    }

    private static async Task<IdempotencyLedgerBeginResult> BeginSharedAsync(
        SqliteFileTestDatabase database,
        OrderedInsertCoordinator coordinator,
        int participant)
    {
        var interceptor = new CoordinatedAddedEntityInterceptor<IdempotencyLedger>(coordinator, participant);
        await using var db = database.CreateContext(interceptor);
        var service = CreateSharedService(db);

        try
        {
            return await service.BeginOrGetExistingAsync(
                "shared-race-user",
                QuizOperationTypes.QuizAnswer,
                "shared-race-operation",
                "shared-race-key",
                "/api/quiz/answer",
                new { questionId = 1, answer = "2" });
        }
        finally
        {
            if (participant == 1)
                coordinator.ReleaseSecondWriter();
        }
    }

    private static async Task<EconomyTransactionBeginResult> BeginEconomyAsync(
        SqliteFileTestDatabase database,
        OrderedInsertCoordinator coordinator,
        int participant)
    {
        var interceptor = new CoordinatedAddedEntityInterceptor<EconomyTransaction>(coordinator, participant);
        await using var db = database.CreateContext(interceptor);
        var service = CreateEconomyService(db);

        try
        {
            return await service.BeginOrGetExistingAsync(
                "economy-race-user",
                "coins_spend",
                "economy-race-key",
                new { amount = 10, reason = "hint" },
                operationId: "economy-race-operation");
        }
        finally
        {
            if (participant == 1)
                coordinator.ReleaseSecondWriter();
        }
    }

    private static IdempotencyLedgerService CreateSharedService(ApiDbContext db) =>
        new(
            db,
            NullLogger<IdempotencyLedgerService>.Instance,
            new IdempotencyObservabilityService(NullLogger<IdempotencyObservabilityService>.Instance));

    private static EconomyTransactionService CreateEconomyService(ApiDbContext db) =>
        new(
            db,
            NullLogger<EconomyTransactionService>.Instance,
            new IdempotencyObservabilityService(NullLogger<IdempotencyObservabilityService>.Instance));

    private sealed class CoordinatedAddedEntityInterceptor<TEntity> : SaveChangesInterceptor
        where TEntity : class
    {
        private readonly OrderedInsertCoordinator coordinator;
        private readonly int participant;
        private int used;

        public CoordinatedAddedEntityInterceptor(OrderedInsertCoordinator coordinator, int participant)
        {
            this.coordinator = coordinator;
            this.participant = participant;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref used, 1) == 0 && HasAddedEntity(eventData.Context))
                await coordinator.ArriveAndWaitAsync(participant, cancellationToken);

            return result;
        }

        private static bool HasAddedEntity(DbContext? context) =>
            context?.ChangeTracker.Entries<TEntity>().Any(x => x.State == EntityState.Added) == true;
    }

    private sealed class OrderedInsertCoordinator
    {
        private readonly TaskCompletionSource<bool> bothWritersArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> releaseSecondWriter =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public async Task ArriveAndWaitAsync(int participant, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref arrivals) == 2)
                bothWritersArrived.TrySetResult(true);

            await bothWritersArrived.Task.WaitAsync(cancellationToken);

            if (participant == 2)
                await releaseSecondWriter.Task.WaitAsync(cancellationToken);
        }

        public void ReleaseSecondWriter() => releaseSecondWriter.TrySetResult(true);
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

        public static async Task<SqliteFileTestDatabase> CreateAsync()
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"mathlearning-idempotency-{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = filePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = false,
                DefaultTimeout = 30
            }.ToString();

            var database = new SqliteFileTestDatabase(filePath, connectionString);
            await using var setup = database.CreateContext();
            await setup.Database.EnsureCreatedAsync();
            await setup.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            await setup.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=30000;");
            return database;
        }

        public ApiDbContext CreateContext(params IInterceptor[] interceptors)
        {
            var builder = new DbContextOptionsBuilder<ApiDbContext>()
                .UseSqlite(connectionString);

            if (interceptors.Length > 0)
                builder.AddInterceptors(interceptors);

            return new ApiDbContext(builder.Options);
        }

        public async Task SeedUserAsync(string userId, int coins)
        {
            await using var db = CreateContext();
            db.Users.Add(new IdentityUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@example.test"
            });
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Username = userId,
                DisplayName = userId,
                Coins = coins,
                Level = 1,
                Xp = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        public ValueTask DisposeAsync()
        {
            DeleteIfExists(filePath);
            DeleteIfExists($"{filePath}-wal");
            DeleteIfExists($"{filePath}-shm");
            return ValueTask.CompletedTask;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
