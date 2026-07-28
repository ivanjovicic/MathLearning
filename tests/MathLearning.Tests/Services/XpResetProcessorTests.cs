using System.Collections.Concurrent;
using System.Data.Common;
using MathLearning.Api;
using MathLearning.Api.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class XpResetProcessorTests
{
    [Theory]
    [InlineData("2026-07-21T10:00:00Z", "2026-07-21T23:59:59Z", false, false, false)]
    public void Create_SameUtcDayDoesNotResetAgain(
        string lastReset,
        string now,
        bool expectedDaily,
        bool expectedWeekly,
        bool expectedMonthly)
    {
        var window = XpResetWindow.Create(
            DateTime.Parse(now, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
            DateTime.Parse(lastReset, null, System.Globalization.DateTimeStyles.AdjustToUniversal));

        Assert.Equal(expectedDaily, window.ResetDaily);
        Assert.Equal(expectedWeekly, window.ResetWeekly);
        Assert.Equal(expectedMonthly, window.ResetMonthly);
    }

    [Fact]
    public void Create_MondayUtcBoundaryAdvancesDailyAndWeeklyWindows()
    {
        var window = XpResetWindow.Create(
            DateTime.Parse("2026-07-20T00:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal),
            DateTime.Parse("2026-07-19T23:59:59Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal));

        Assert.True(window.ResetDaily);
        Assert.True(window.ResetWeekly);
        Assert.False(window.ResetMonthly);
    }

    [Fact]
    public void Create_NewYearUtcBoundaryAdvancesAllWindows()
    {
        var window = XpResetWindow.Create(
            DateTime.Parse("2026-01-01T00:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal),
            DateTime.Parse("2025-12-31T23:59:59Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal));

        Assert.True(window.ResetDaily);
        Assert.False(window.ResetWeekly);
        Assert.True(window.ResetMonthly);
    }

    [Fact]
    public async Task RunOnceAsync_SkipsWhenSchemaIsNotReadyWithoutTouchingProfiles()
    {
        await using var factory = await CreateFactoryAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var schemaState = scope.ServiceProvider.GetRequiredService<DatabaseSchemaState>();
        schemaState.Update(DatabaseSchemaStatus.NotChecked);

        var processor = BuildProcessor(db, schemaState, new AllowAllLease(), new MutableTimeProvider(DateTimeOffset.Parse("2026-07-28T00:00:00Z")));
        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal("schema-not-ready", result.Status);
        Assert.False(result.LockAcquired);
        Assert.Equal(0, result.RowsAffected);
    }

    [Fact]
    public async Task RunOnceAsync_OnlyOneWorkerAcquiresAdvisoryLock()
    {
        await using var factory = await CreateFactoryAsync();
        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<ApiDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApiDbContext>();
        var schemaState1 = scope1.ServiceProvider.GetRequiredService<DatabaseSchemaState>();
        var schemaState2 = scope2.ServiceProvider.GetRequiredService<DatabaseSchemaState>();
        var lease = new PostgresXpResetOwnershipLease();

        var ready = ReadySchemaStatus();
        schemaState1.Update(ready);
        schemaState2.Update(ready);

        await using var tx1 = await db1.Database.BeginTransactionAsync();
        await using var tx2 = await db2.Database.BeginTransactionAsync();

        Assert.True(await lease.TryAcquireAsync(db1, CancellationToken.None));
        Assert.False(await lease.TryAcquireAsync(db2, CancellationToken.None));

        await tx1.RollbackAsync();
        await tx2.RollbackAsync();
    }

    [Fact]
    public async Task RunOnceAsync_RestartAfterSuccessDoesNotRepeatDestructiveWork()
    {
        await using var factory = await CreateFactoryAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var schemaState = scope.ServiceProvider.GetRequiredService<DatabaseSchemaState>();
        schemaState.Update(ReadySchemaStatus());

        await db.Database.ExecuteSqlRawAsync("""
UPDATE "UserProfiles"
SET "DailyXp" = 18,
    "WeeklyXp" = 42,
    "MonthlyXp" = 90,
    "LastXpResetDate" = TIMESTAMPTZ '2026-06-30 12:00:00+00',
    "UpdatedAt" = NOW()
WHERE "UserId" = '1';
""");

        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-28T06:00:00Z"));
        var processor = BuildProcessor(db, schemaState, new AllowAllLease(), clock);

        var first = await processor.RunOnceAsync(CancellationToken.None);
        Assert.Equal("applied", first.Status);
        Assert.True(first.RowsAffected > 0);

        var second = await processor.RunOnceAsync(CancellationToken.None);
        Assert.Equal("applied", second.Status);
        Assert.Equal(0, second.RowsAffected);

        var reloaded = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == "1");
        Assert.Equal(0, reloaded.DailyXp);
        Assert.Equal(0, reloaded.WeeklyXp);
        Assert.Equal(0, reloaded.MonthlyXp);
    }

    [Fact]
    public async Task RunOnceAsync_ConcurrentAwardPreservesAuthoritativeTotals()
    {
        await using var factory = await CreateFactoryAsync();
        using var seedScope = factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var schemaState = seedScope.ServiceProvider.GetRequiredService<DatabaseSchemaState>();
        schemaState.Update(ReadySchemaStatus());

        await seedDb.Database.ExecuteSqlRawAsync("""
UPDATE "UserProfiles"
SET "Xp" = 0,
    "DailyXp" = 5,
    "WeeklyXp" = 5,
    "MonthlyXp" = 5,
    "LastXpResetDate" = TIMESTAMPTZ '2026-07-26 12:00:00+00',
    "UpdatedAt" = NOW()
WHERE "UserId" = '1';
""");

        var seeded = await seedDb.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == "1");
        Assert.Equal(0, seeded.Xp);
        Assert.Equal(5, seeded.DailyXp);
        Assert.Equal(5, seeded.WeeklyXp);
        Assert.Equal(5, seeded.MonthlyXp);

        using var resetScope = factory.Services.CreateScope();
        using var awardScope = factory.Services.CreateScope();
        var resetDb = resetScope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var awardDb = awardScope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var resetSchema = resetScope.ServiceProvider.GetRequiredService<DatabaseSchemaState>();
        resetSchema.Update(ReadySchemaStatus());
        var gate = new HoldAfterRowLockInterceptor();
        await using var gatedAwardDb = CreateInterceptedDb(awardDb.Database.GetConnectionString()!, gate);
        var resetProcessor = BuildProcessor(
            resetDb,
            resetSchema,
            new AllowAllLease(),
            new MutableTimeProvider(DateTimeOffset.Parse("2026-07-28T08:00:00Z")));
        var awardService = new XpTrackingService(
            gatedAwardDb,
            Microsoft.Extensions.Options.Options.Create(new XpTrackingOptions()),
            NullLogger<XpTrackingService>.Instance);

        var awardTask = awardService.AddXpWithinTransactionAsync("1", 10, false, "integration_test", gatedAwardDb, CancellationToken.None);
        await gate.RowLockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var resetTask = resetProcessor.RunOnceAsync(CancellationToken.None);
        gate.Release();
        await Task.WhenAll(resetTask, awardTask);

        var reloaded = await seedDb.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == "1");
        Assert.Equal(10, reloaded.Xp);
        Assert.Equal(10, reloaded.DailyXp);
        Assert.Equal(10, reloaded.WeeklyXp);
        Assert.Equal(15, reloaded.MonthlyXp);
    }

    [Fact]
    public async Task RunOnceAsync_PassesCancellationTokenToDatabaseCommands()
    {
        await using var factory = await CreateFactoryAsync();
        using var scope = factory.Services.CreateScope();
        var templateDb = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var schemaState = scope.ServiceProvider.GetRequiredService<DatabaseSchemaState>();
        schemaState.Update(ReadySchemaStatus());

        var interceptor = new RecordingDbCommandInterceptor();
        await using var db = CreateInterceptedDb(templateDb.Database.GetConnectionString()!, interceptor);

        var cts = new CancellationTokenSource();
        var processor = BuildProcessor(db, schemaState, new AllowAllLease(), new MutableTimeProvider(DateTimeOffset.Parse("2026-07-28T08:00:00Z")));
        await processor.RunOnceAsync(cts.Token);

        Assert.Contains(interceptor.CancellationTokens, token => token.Equals(cts.Token));
        Assert.True(interceptor.TotalCommands > 0);
    }

    [Fact]
    public async Task RunOnceAsync_100kFixtureUsesFixedSmallSqlCount()
    {
        await using var factory = await CreateFactoryAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var schemaState = scope.ServiceProvider.GetRequiredService<DatabaseSchemaState>();
        schemaState.Update(ReadySchemaStatus());

        var counter = new CountingDbCommandInterceptor();
        await using var observedDb = CreateInterceptedDb(db.Database.GetConnectionString()!, counter);

        await SeedLargeFixtureAsync(db, 100_000);
        counter.Reset();

        var processor = BuildProcessor(observedDb, schemaState, new AllowAllLease(), new MutableTimeProvider(DateTimeOffset.Parse("2026-07-28T08:00:00Z")));
        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal("applied", result.Status);
        Assert.Equal(2, counter.TotalCommands);
        Assert.True(result.RowsAffected >= 100_000);
        Assert.Empty(observedDb.ChangeTracker.Entries());
    }

    [Fact]
    public async Task RunOnceAsync_RollsBackWhenDatabaseCommandFails()
    {
        await using var factory = await CreateFactoryAsync();
        using var scope = factory.Services.CreateScope();
        var templateDb = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var schemaState = scope.ServiceProvider.GetRequiredService<DatabaseSchemaState>();
        schemaState.Update(ReadySchemaStatus());

        await templateDb.Database.ExecuteSqlRawAsync("""
UPDATE "UserProfiles"
SET "DailyXp" = 14,
    "WeeklyXp" = 14,
    "MonthlyXp" = 14,
    "LastXpResetDate" = TIMESTAMPTZ '2026-07-26 12:00:00+00',
    "UpdatedAt" = NOW()
WHERE "UserId" = '1';
""");

        var failingInterceptor = new ThrowOnNextNonQueryInterceptor();
        await using var db = CreateInterceptedDb(templateDb.Database.GetConnectionString()!, failingInterceptor);

        var processor = BuildProcessor(db, schemaState, new AllowAllLease(), new MutableTimeProvider(DateTimeOffset.Parse("2026-07-28T08:00:00Z")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.RunOnceAsync(CancellationToken.None));

        var reloaded = await templateDb.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == "1");
        Assert.Equal(14, reloaded.DailyXp);
        Assert.Equal(14, reloaded.WeeklyXp);
        Assert.Equal(14, reloaded.MonthlyXp);
    }

    private static XpResetProcessor BuildProcessor(
        ApiDbContext db,
        DatabaseSchemaState schemaState,
        IXpResetOwnershipLease lease,
        TimeProvider timeProvider)
        => new(
            db,
            schemaState,
            lease,
            timeProvider,
            NullLogger<XpResetProcessor>.Instance);

    private static DatabaseSchemaStatus ReadySchemaStatus() =>
        new(
            "Ready",
            true,
            LatestCodeMigration: null,
            LatestAppliedMigration: null,
            PendingMigrations: Array.Empty<string>(),
            UnknownAppliedMigrations: Array.Empty<string>(),
            FailureMessage: null,
            CheckedAtUtc: DateTime.UtcNow);

    private static async Task SeedLargeFixtureAsync(ApiDbContext db, int count)
    {
        var maxId = 1_000_000 + count - 1;

        await db.Database.ExecuteSqlRawAsync($"""
INSERT INTO "AspNetUsers" ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
SELECT uid::text,
       'user-' || uid::text,
       'USER-' || uid::text,
       NULL,
       NULL,
       FALSE,
       NULL,
       uid::text,
       uid::text,
       FALSE,
       FALSE,
       FALSE,
       0
FROM generate_series(1000000, {maxId}) AS uid
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "UserProfiles" ("UserId", "Username", "DisplayName", "Coins", "TotalCoinsEarned", "TotalCoinsSpent", "Level", "Xp", "Streak", "DailyXp", "WeeklyXp", "MonthlyXp", "LastXpResetDate", "LeaderboardOptIn", "CreatedAt", "UpdatedAt")
SELECT uid::text,
       'user-' || uid::text,
       'User ' || uid::text,
       100,
       0,
       0,
       1,
       0,
       0,
       3,
       4,
       5,
       TIMESTAMPTZ '2026-07-27 12:00:00+00',
       TRUE,
       NOW(),
       NOW()
FROM generate_series(1000000, {maxId}) AS uid
ON CONFLICT ("UserId") DO NOTHING;
""");

        await db.SaveChangesAsync();
    }

    private static async Task<XpResetPostgresWebApplicationFactory> CreateFactoryAsync()
    {
        var database = await PostgresTestDatabase.CreateAsync();
        return new XpResetPostgresWebApplicationFactory(database);
    }

    private static ApiDbContext CreateInterceptedDb(string connectionString, DbCommandInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(interceptor)
            .Options;

        return new ApiDbContext(options);
    }

    private sealed class XpResetPostgresWebApplicationFactory : PostgresWebApplicationFactory<Program>
    {
        public XpResetPostgresWebApplicationFactory(PostgresTestDatabase database)
            : base(database)
        {
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void SetUtcNow(DateTimeOffset value) => utcNow = value;
    }

    private sealed class CountingDbCommandInterceptor : DbCommandInterceptor
    {
        private int totalCommands;

        public int TotalCommands => Volatile.Read(ref totalCommands);

        public void Reset() => Interlocked.Exchange(ref totalCommands, 0);

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            Interlocked.Increment(ref totalCommands);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref totalCommands);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result)
        {
            Interlocked.Increment(ref totalCommands);
            return base.ScalarExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref totalCommands);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class RecordingDbCommandInterceptor : DbCommandInterceptor
    {
        private int totalCommands;
        public ConcurrentBag<CancellationToken> CancellationTokens { get; } = new();
        public int TotalCommands => Volatile.Read(ref totalCommands);

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            Interlocked.Increment(ref totalCommands);
            CancellationTokens.Add(CancellationToken.None);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref totalCommands);
            CancellationTokens.Add(cancellationToken);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result)
        {
            Interlocked.Increment(ref totalCommands);
            CancellationTokens.Add(CancellationToken.None);
            return base.ScalarExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref totalCommands);
            CancellationTokens.Add(cancellationToken);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class ThrowOnNextNonQueryInterceptor : DbCommandInterceptor
    {
        private int throwArmed = 1;

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            if (Interlocked.Exchange(ref throwArmed, 0) == 1)
            {
                throw new InvalidOperationException("xp reset failure sentinel");
            }

            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref throwArmed, 0) == 1)
            {
                throw new InvalidOperationException("xp reset failure sentinel");
            }

            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class HoldAfterRowLockInterceptor : DbCommandInterceptor
    {
        public TaskCompletionSource RowLockAcquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => release.TrySetResult();

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("\"UserProfiles\"", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                RowLockAcquired.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
            }

            return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class AllowAllLease : IXpResetOwnershipLease
    {
        public Task<bool> TryAcquireAsync(ApiDbContext db, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
