using MathLearning.Application.Services;
using MathLearning.Infrastructure.Services;
using MathLearning.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class EconomyTransactionServiceTests
{
    [Fact]
    public async Task FirstRequest_CreatesTransaction()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new EconomyTransactionService(db, NullLogger<EconomyTransactionService>.Instance);

        var begin = await service.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 10 });

        Assert.True(begin.ShouldProcess);
        Assert.False(begin.IsExisting);
        Assert.True(begin.IsPending);
    }

    [Fact]
    public async Task SameIdempotencyKey_ReturnsExistingResult()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new EconomyTransactionService(db, NullLogger<EconomyTransactionService>.Instance);

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
        var service = new EconomyTransactionService(db, NullLogger<EconomyTransactionService>.Instance);

        await service.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 10 });

        await Assert.ThrowsAsync<EconomyTransactionConflictException>(() =>
            service.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 99 }));
    }

    [Fact]
    public async Task DifferentUsers_CanUseSameIdempotencyKey()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new EconomyTransactionService(db, NullLogger<EconomyTransactionService>.Instance);

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
        var service = new EconomyTransactionService(db, NullLogger<EconomyTransactionService>.Instance);

        var begin = await service.BeginOrGetExistingAsync("u1", "coins_spend", "k1", new { amount = 10 });
        var complete = await service.CompleteAsync(begin.TransactionId, new { success = true, coins = 90 });
        var completeAgain = await service.CompleteAsync(begin.TransactionId, new { success = true, coins = 90 });

        Assert.True(complete.IsCompleted);
        Assert.True(completeAgain.IsCompleted);
        Assert.Equal(complete.TransactionId, completeAgain.TransactionId);
        Assert.Equal(complete.ResultJson, completeAgain.ResultJson);
    }
}
