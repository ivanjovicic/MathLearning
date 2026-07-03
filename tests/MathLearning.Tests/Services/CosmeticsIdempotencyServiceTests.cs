using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Cosmetics;
using MathLearning.Infrastructure.Services.Idempotency;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class CosmeticsIdempotencyServiceTests
{
    [Fact]
    public async Task FirstRequest_CreatesPendingLedger_WithCanonicalPayloadAndTrimmedKeys()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync(
            "  user-1  ",
            "  cosmetics_item_claim  ",
            "  operation-1  ",
            "  key-1  ",
            new { sourceEvent = "daily_run", itemKey = "frame_comet" });

        Assert.False(begin.IsExisting);
        Assert.True(begin.ShouldProcess);
        Assert.True(begin.IsPending);
        Assert.Equal("user-1", begin.Ledger.UserId);
        Assert.Equal("cosmetics_item_claim", begin.Ledger.OperationType);
        Assert.Equal("operation-1", begin.Ledger.OperationId);
        Assert.Equal("key-1", begin.Ledger.IdempotencyKey);

        var persisted = await db.CosmeticsIdempotencyLedgers.SingleAsync();
        Assert.Equal("{\"itemKey\":\"frame_comet\",\"sourceEvent\":\"daily_run\"}", persisted.RequestJson);
        Assert.Equal(CosmeticsIdempotencyStatuses.Pending, persisted.Status);
    }

    [Fact]
    public async Task EquivalentPayloadWithDifferentPropertyOrder_ReplaysCompletedResult()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var first = await service.BeginOrGetExistingAsync(
            "user-1",
            "cosmetics_fragment_grant",
            "operation-1",
            "key-1",
            new Dictionary<string, object?> { ["copies"] = 2, ["fragmentName"] = "Comet Frame Fragment" });
        var completed = await service.CompleteAsync(
            first.LedgerId,
            new Dictionary<string, object?> { ["itemUnlocked"] = false, ["collectedFragments"] = 2 });

        var replay = await service.BeginOrGetExistingAsync(
            "user-1",
            "cosmetics_fragment_grant",
            "operation-1",
            "key-1",
            new Dictionary<string, object?> { ["fragmentName"] = "Comet Frame Fragment", ["copies"] = 2 });

        Assert.True(replay.IsExisting);
        Assert.False(replay.ShouldProcess);
        Assert.True(replay.IsCompleted);
        Assert.Equal(completed.ResultJson, replay.ResultJson);
        Assert.Equal("{\"collectedFragments\":2,\"itemUnlocked\":false}", replay.ResultJson);
    }

    [Fact]
    public async Task SameKeysWithDifferentPayload_ThrowsConflictWithoutCreatingAnotherLedger()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await service.BeginOrGetExistingAsync(
            "user-1", "cosmetics_fragment_grant", "operation-1", "key-1", new { copies = 1 });

        var conflict = await Assert.ThrowsAsync<CosmeticsIdempotencyConflictException>(() =>
            service.BeginOrGetExistingAsync(
                "user-1", "cosmetics_fragment_grant", "operation-1", "key-1", new { copies = 2 }));

        Assert.Equal("user-1", conflict.UserId);
        Assert.Equal("operation-1", conflict.OperationId);
        Assert.Equal("key-1", conflict.IdempotencyKey);
        Assert.Equal(1, await db.CosmeticsIdempotencyLedgers.CountAsync());
    }

    [Fact]
    public async Task SameOperationIdWithDifferentIdempotencyKey_ThrowsConflict()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await service.BeginOrGetExistingAsync(
            "user-1", "cosmetics_item_claim", "operation-1", "key-1", new { itemKey = "frame_comet" });

        await Assert.ThrowsAsync<CosmeticsIdempotencyConflictException>(() =>
            service.BeginOrGetExistingAsync(
                "user-1", "cosmetics_item_claim", "operation-1", "key-2", new { itemKey = "frame_comet" }));
    }

    [Fact]
    public async Task SameIdempotencyKeyWithDifferentOperationId_ThrowsConflict()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await service.BeginOrGetExistingAsync(
            "user-1", "cosmetics_item_claim", "operation-1", "key-1", new { itemKey = "frame_comet" });

        await Assert.ThrowsAsync<CosmeticsIdempotencyConflictException>(() =>
            service.BeginOrGetExistingAsync(
                "user-1", "cosmetics_item_claim", "operation-2", "key-1", new { itemKey = "frame_comet" }));
    }

    [Fact]
    public async Task SameKeys_AreIsolatedByUser()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var first = await service.BeginOrGetExistingAsync(
            "user-1", "cosmetics_item_claim", "operation-1", "key-1", new { itemKey = "frame_comet" });
        var second = await service.BeginOrGetExistingAsync(
            "user-2", "cosmetics_item_claim", "operation-1", "key-1", new { itemKey = "frame_comet" });

        Assert.True(first.ShouldProcess);
        Assert.True(second.ShouldProcess);
        Assert.NotEqual(first.LedgerId, second.LedgerId);
        Assert.Equal(2, await db.CosmeticsIdempotencyLedgers.CountAsync());
    }

    [Fact]
    public async Task FailedLedger_ReplaysStoredFailureWithoutProcessingAgain()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync(
            "user-1", "cosmetics_item_claim", "operation-1", "key-1", new { itemKey = "missing_item" });
        var failed = await service.FailAsync(
            begin.LedgerId,
            "invalid_item",
            new { success = false, message = "Item was not found" });

        var replay = await service.BeginOrGetExistingAsync(
            "user-1", "cosmetics_item_claim", "operation-1", "key-1", new { itemKey = "missing_item" });

        Assert.True(failed.IsFailed);
        Assert.True(replay.IsExisting);
        Assert.False(replay.ShouldProcess);
        Assert.True(replay.IsFailed);
        Assert.Equal("invalid_item", replay.ErrorCode);
        Assert.Equal(failed.ResultJson, replay.ResultJson);
    }

    [Fact]
    public async Task Complete_IsIdempotentForEquivalentResult_ButRejectsDifferentResult()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync(
            "user-1", "cosmetics_fragment_grant", "operation-1", "key-1", new { copies = 2 });
        var first = await service.CompleteAsync(
            begin.LedgerId,
            new Dictionary<string, object?> { ["itemUnlocked"] = false, ["collectedFragments"] = 2 });
        var same = await service.CompleteAsync(
            begin.LedgerId,
            new Dictionary<string, object?> { ["collectedFragments"] = 2, ["itemUnlocked"] = false });

        Assert.Equal(first.ResultJson, same.ResultJson);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(begin.LedgerId, new { itemUnlocked = true, collectedFragments = 5 }));
    }

    [Fact]
    public async Task Fail_IsIdempotentForEquivalentFailure_ButRejectsDifferentFailure()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync(
            "user-1", "cosmetics_item_claim", "operation-1", "key-1", new { itemKey = "missing_item" });
        var first = await service.FailAsync(
            begin.LedgerId,
            "invalid_item",
            new Dictionary<string, object?> { ["message"] = "Item was not found", ["success"] = false });
        var same = await service.FailAsync(
            begin.LedgerId,
            "invalid_item",
            new Dictionary<string, object?> { ["success"] = false, ["message"] = "Item was not found" });

        Assert.Equal(first.ResultJson, same.ResultJson);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FailAsync(begin.LedgerId, "different_error", new { success = false }));
    }

    [Fact]
    public async Task CompletedLedgerCannotFail_AndFailedLedgerCannotComplete()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var completedBegin = await service.BeginOrGetExistingAsync(
            "user-1", "cosmetics_item_claim", "operation-completed", "key-completed", new { itemKey = "frame_comet" });
        await service.CompleteAsync(completedBegin.LedgerId, new { success = true });

        var failedBegin = await service.BeginOrGetExistingAsync(
            "user-1", "cosmetics_item_claim", "operation-failed", "key-failed", new { itemKey = "missing_item" });
        await service.FailAsync(failedBegin.LedgerId, "invalid_item", new { success = false });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FailAsync(completedBegin.LedgerId, "late_failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(failedBegin.LedgerId, new { success = true }));
    }

    [Fact]
    public async Task CompleteAndFail_UnknownLedger_ThrowClearErrors()
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

    [Fact]
    public async Task Begin_RejectsBlankRequiredScopeValues()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BeginOrGetExistingAsync(" ", "cosmetics_item_claim", "operation", "key", new { }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BeginOrGetExistingAsync("user", " ", "operation", "key", new { }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BeginOrGetExistingAsync("user", "cosmetics_item_claim", " ", "key", new { }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BeginOrGetExistingAsync("user", "cosmetics_item_claim", "operation", " ", new { }));
    }

    private static CosmeticsIdempotencyService CreateService(ApiDbContext db)
        => new(
            db,
            NullLogger<CosmeticsIdempotencyService>.Instance,
            new IdempotencyObservabilityService(NullLogger<IdempotencyObservabilityService>.Instance));
}
