using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Idempotency;
using MathLearning.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class EconomyTransactionStateMachineTests
{
    [Fact]
    public async Task FailedTransaction_ReplaysStoredFailureWithoutProcessingAgain()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync(
            "user-1",
            "coins_spend",
            "failure-key",
            new { amount = 10 });

        var failed = await service.FailAsync(
            begin.TransactionId,
            "insufficient_balance",
            new { success = false, balance = 5 });

        var replay = await service.BeginOrGetExistingAsync(
            "user-1",
            "coins_spend",
            "failure-key",
            new { amount = 10 });

        Assert.True(failed.IsFailed);
        Assert.True(replay.IsExisting);
        Assert.False(replay.ShouldProcess);
        Assert.True(replay.IsFailed);
        Assert.Equal("insufficient_balance", replay.ErrorCode);
        Assert.Equal(failed.ResultJson, replay.ResultJson);
    }

    [Fact]
    public async Task FailedTransaction_CannotBeCompleted()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync(
            "user-1",
            "coins_spend",
            "failed-cannot-complete",
            new { amount = 10 });
        await service.FailAsync(begin.TransactionId, "insufficient_balance", new { success = false });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(begin.TransactionId, new { success = true }));
    }

    [Fact]
    public async Task CompletedTransaction_CannotBeFailed()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync(
            "user-1",
            "coins_spend",
            "completed-cannot-fail",
            new { amount = 10 });
        await service.CompleteAsync(begin.TransactionId, new { success = true, balance = 90 });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FailAsync(begin.TransactionId, "late_failure", new { success = false }));
    }

    [Fact]
    public async Task CompletingTwice_WithDifferentResultPayload_Throws()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync(
            "user-1",
            "coins_spend",
            "complete-payload-conflict",
            new { amount = 10 });
        await service.CompleteAsync(begin.TransactionId, new { success = true, balance = 90 });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(begin.TransactionId, new { success = true, balance = 80 }));
    }

    [Fact]
    public async Task FailingTwice_WithDifferentFailurePayload_Throws()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync(
            "user-1",
            "coins_spend",
            "failure-payload-conflict",
            new { amount = 10 });
        await service.FailAsync(
            begin.TransactionId,
            "insufficient_balance",
            new { success = false, balance = 5 });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FailAsync(
                begin.TransactionId,
                "different_error",
                new { success = false, balance = 4 }));
    }

    [Fact]
    public async Task SameIdempotencyKey_DifferentTransactionTypes_AreIsolated()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var spend = await service.BeginOrGetExistingAsync(
            "user-1",
            "coins_spend",
            "shared-key",
            new { amount = 10 });
        var reward = await service.BeginOrGetExistingAsync(
            "user-1",
            "reward_claim",
            "shared-key",
            new { rewardId = "daily:lesson-complete" });

        Assert.True(spend.ShouldProcess);
        Assert.True(reward.ShouldProcess);
        Assert.NotEqual(spend.TransactionId, reward.TransactionId);
    }

    [Fact]
    public async Task SameOperationId_WithDifferentIdempotencyKey_ThrowsConflict()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await service.BeginOrGetExistingAsync(
            "user-1",
            "cosmetics_fragment_grant",
            "key-1",
            new { fragmentName = "Comet Frame Fragment", copies = 1 },
            operationId: "operation-1");

        await Assert.ThrowsAsync<EconomyTransactionConflictException>(() =>
            service.BeginOrGetExistingAsync(
                "user-1",
                "cosmetics_fragment_grant",
                "key-2",
                new { fragmentName = "Comet Frame Fragment", copies = 1 },
                operationId: "operation-1"));
    }

    [Fact]
    public async Task SameIdempotencyKey_WithDifferentOperationId_ThrowsConflict()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await service.BeginOrGetExistingAsync(
            "user-1",
            "cosmetics_fragment_grant",
            "shared-key",
            new { fragmentName = "Comet Frame Fragment", copies = 1 },
            operationId: "operation-1");

        await Assert.ThrowsAsync<EconomyTransactionConflictException>(() =>
            service.BeginOrGetExistingAsync(
                "user-1",
                "cosmetics_fragment_grant",
                "shared-key",
                new { fragmentName = "Comet Frame Fragment", copies = 1 },
                operationId: "operation-2"));
    }

    [Fact]
    public async Task Begin_RejectsWhitespaceRequiredValues()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BeginOrGetExistingAsync(" ", "coins_spend", "key", new { amount = 10 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BeginOrGetExistingAsync("user-1", " ", "key", new { amount = 10 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BeginOrGetExistingAsync("user-1", "coins_spend", " ", new { amount = 10 }));
    }

    [Fact]
    public async Task CompleteAndFail_UnknownTransaction_ThrowClearErrors()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var missingId = Guid.NewGuid();

        var completeError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(missingId, new { success = true }));
        var failError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FailAsync(missingId, "not_found"));

        Assert.Contains(missingId.ToString(), completeError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(missingId.ToString(), failError.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static EconomyTransactionService CreateService(ApiDbContext db)
        => new(
            db,
            NullLogger<EconomyTransactionService>.Instance,
            new IdempotencyObservabilityService(NullLogger<IdempotencyObservabilityService>.Instance));
}
