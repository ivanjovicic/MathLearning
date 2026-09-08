using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Idempotency;

public sealed class RelationalIdempotencyConstraintTests
{
    [Fact]
    public async Task EconomyTransaction_DuplicateUserTypeAndKey_IsRejectedByDatabase()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        db.EconomyTransactions.Add(NewEconomyTransaction("user-1", "coins_spend", "key-1", "op-1"));
        await db.SaveChangesAsync();

        db.EconomyTransactions.Add(NewEconomyTransaction("user-1", "coins_spend", "key-1", "op-2"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task EconomyTransaction_DuplicateUserTypeAndOperation_IsRejectedByDatabase()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        db.EconomyTransactions.Add(NewEconomyTransaction("user-1", "coins_spend", "key-1", "op-1"));
        await db.SaveChangesAsync();

        db.EconomyTransactions.Add(NewEconomyTransaction("user-1", "coins_spend", "key-2", "op-1"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task EconomyTransaction_SameKeysForDifferentUsers_AreAllowed()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        db.EconomyTransactions.AddRange(
            NewEconomyTransaction("user-1", "coins_spend", "shared-key", "shared-op"),
            NewEconomyTransaction("user-2", "coins_spend", "shared-key", "shared-op"));

        await db.SaveChangesAsync();

        Assert.Equal(2, await db.EconomyTransactions.CountAsync());
    }

    [Fact]
    public async Task EconomyTransaction_MultipleNullOperationIds_AreAllowedWhenKeysDiffer()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        db.EconomyTransactions.AddRange(
            NewEconomyTransaction("user-1", "coins_spend", "key-1", operationId: null),
            NewEconomyTransaction("user-1", "coins_spend", "key-2", operationId: null));

        await db.SaveChangesAsync();

        Assert.Equal(2, await db.EconomyTransactions.CountAsync());
    }

    [Fact]
    public async Task DailyRunChestClaim_DuplicateUserAndDay_IsRejectedByDatabase()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var day = new DateOnly(2026, 7, 2);

        db.DailyRunChestClaims.Add(NewChestClaim("user-1", day, "tx-1"));
        await db.SaveChangesAsync();

        db.DailyRunChestClaims.Add(NewChestClaim("user-1", day, "tx-2"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DailyRunChestClaim_DuplicateUserAndTransaction_IsRejectedByDatabase()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        db.DailyRunChestClaims.Add(NewChestClaim("user-1", new DateOnly(2026, 7, 2), "shared-tx"));
        await db.SaveChangesAsync();

        db.DailyRunChestClaims.Add(NewChestClaim("user-1", new DateOnly(2026, 7, 3), "shared-tx"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DailyRunChestClaim_SameTransactionForDifferentUsers_IsAllowed()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        db.DailyRunChestClaims.AddRange(
            NewChestClaim("user-1", new DateOnly(2026, 7, 2), "shared-tx"),
            NewChestClaim("user-2", new DateOnly(2026, 7, 2), "shared-tx"));

        await db.SaveChangesAsync();

        Assert.Equal(2, await db.DailyRunChestClaims.CountAsync());
    }

    [Fact]
    public async Task CosmeticsLedger_DuplicateUserAndOperation_IsRejectedByDatabase()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        db.CosmeticsIdempotencyLedgers.Add(NewCosmeticsLedger("user-1", "op-1", "key-1"));
        await db.SaveChangesAsync();

        db.CosmeticsIdempotencyLedgers.Add(NewCosmeticsLedger("user-1", "op-1", "key-2"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task CosmeticsLedger_DuplicateUserAndIdempotencyKey_IsRejectedByDatabase()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        db.CosmeticsIdempotencyLedgers.Add(NewCosmeticsLedger("user-1", "op-1", "key-1"));
        await db.SaveChangesAsync();

        db.CosmeticsIdempotencyLedgers.Add(NewCosmeticsLedger("user-1", "op-2", "key-1"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static EconomyTransaction NewEconomyTransaction(
        string userId,
        string transactionType,
        string idempotencyKey,
        string? operationId)
        => new()
        {
            UserId = userId,
            TransactionType = transactionType,
            IdempotencyKey = idempotencyKey,
            OperationId = operationId,
            Status = EconomyTransactionStatus.Pending,
            RequestHash = Guid.NewGuid().ToString("N"),
            RequestJson = "{}",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static DailyRunChestClaim NewChestClaim(string userId, DateOnly day, string transactionId)
        => new()
        {
            UserId = userId,
            Day = day,
            TransactionId = transactionId,
            Xp = 30,
            Coins = 10,
            CosmeticFragment = "Comet Frame Fragment",
            FragmentCopies = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

    private static CosmeticsIdempotencyLedger NewCosmeticsLedger(
        string userId,
        string operationId,
        string idempotencyKey)
        => new()
        {
            UserId = userId,
            OperationId = operationId,
            IdempotencyKey = idempotencyKey,
            OperationType = "cosmetics_fragment_grant",
            PayloadHash = Guid.NewGuid().ToString("N"),
            RequestJson = "{}",
            Status = "pending",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private sealed class SqliteTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<ApiDbContext> options;

        private SqliteTestDatabase(SqliteConnection connection, DbContextOptions<ApiDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<SqliteTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApiDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var setup = new ApiDbContext(options);
            await setup.Database.EnsureCreatedAsync();
            return new SqliteTestDatabase(connection, options);
        }

        public ApiDbContext CreateContext() => new(options);

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }
}
