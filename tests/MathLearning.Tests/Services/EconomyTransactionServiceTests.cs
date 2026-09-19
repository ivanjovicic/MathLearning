using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Idempotency;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class EconomyTransactionServiceTests
{
    [Fact]
    public async Task FirstRequest_CreatesTransaction()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 10 });

        Assert.True(begin.ShouldProcess);
        Assert.False(begin.IsExisting);
        Assert.True(begin.IsPending);
    }

    [Fact]
    public async Task SameIdempotencyKey_ReturnsExistingResult()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var first = await service.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 10 });
        await service.CompleteAsync(first.TransactionId, new { success = true, coins = 90 });

        var retry = await service.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 10 });

        Assert.True(retry.IsExisting);
        Assert.False(retry.ShouldProcess);
        Assert.True(retry.IsCompleted);
        Assert.NotNull(retry.ResultJson);
    }

    [Fact]
    public async Task SameIdempotencyKey_DifferentPayload_ThrowsConflict()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await service.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 10 });

        await Assert.ThrowsAsync<EconomyTransactionConflictException>(() =>
            service.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 99 }));
    }

    [Fact]
    public async Task DifferentUsers_CanUseSameIdempotencyKey()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var first = await service.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 10 });
        var second = await service.BeginOrGetExistingAsync("u2", "coins_spend", "k1", new { amount = 10 });

        Assert.True(first.ShouldProcess);
        Assert.True(second.ShouldProcess);
        Assert.NotEqual(first.TransactionId, second.TransactionId);
    }

    [Fact]
    public async Task CompletedTransaction_DoesNotMutateTwice()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 10 });
        var complete = await service.CompleteAsync(begin.TransactionId, new { success = true, coins = 90 });
        var completeAgain = await service.CompleteAsync(begin.TransactionId, new { success = true, coins = 90 });

        Assert.True(complete.IsCompleted);
        Assert.True(completeAgain.IsCompleted);
        Assert.Equal(complete.TransactionId, completeAgain.TransactionId);
        Assert.Equal(complete.ResultJson, completeAgain.ResultJson);
    }

    [Fact]
    public async Task OperationIdAndIdempotencyKey_ResolveSameLedgerEntry()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var first = await service.BeginOrGetExistingAsync(
            "u1",
            "cosmetics_fragment_grant",
            "daily-run-tx-1",
            new { fragmentName = "Comet Frame Fragment", copies = 1 },
            operationId: "op-1");
        await service.CompleteAsync(first.TransactionId, new { success = true });

        var byOperation = await service.BeginOrGetExistingAsync(
            "u1",
            "cosmetics_fragment_grant",
            "daily-run-tx-1",
            new { fragmentName = "Comet Frame Fragment", copies = 1 },
            operationId: "op-1");
        var byIdempotency = await service.BeginOrGetExistingAsync(
            "u1",
            "cosmetics_fragment_grant",
            "daily-run-tx-1",
            new { fragmentName = "Comet Frame Fragment", copies = 1 },
            operationId: "op-1");

        Assert.False(byOperation.ShouldProcess);
        Assert.False(byIdempotency.ShouldProcess);
        Assert.Equal(first.TransactionId, byOperation.TransactionId);
        Assert.Equal(first.TransactionId, byIdempotency.TransactionId);
    }

    [Fact]
    public async Task StalePendingTransaction_IsTakenOver_AndOldOwnerCannotComplete()
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase($"stale-economy-{Guid.NewGuid():N}")
            .Options;

        await using var firstDb = new ApiDbContext(options);
        var firstService = CreateService(firstDb);
        var first = await firstService.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 10 });

        var pending = await firstDb.EconomyTransactions.SingleAsync();
        pending.UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-10);
        pending.LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5);
        await firstDb.SaveChangesAsync();

        await using var secondDb = new ApiDbContext(options);
        var secondService = CreateService(secondDb);
        var takeover = await secondService.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 10 });

        Assert.True(takeover.IsExisting);
        Assert.True(takeover.ShouldProcess);
        Assert.Equal(first.TransactionId, takeover.TransactionId);

        firstDb.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            firstService.CompleteAsync(first.TransactionId, new { success = true }));
    }

    private static EconomyTransactionService CreateService(ApiDbContext db)
        => new(
            db,
            NullLogger<EconomyTransactionService>.Instance,
            new IdempotencyObservabilityService(NullLogger<IdempotencyObservabilityService>.Instance));
}
