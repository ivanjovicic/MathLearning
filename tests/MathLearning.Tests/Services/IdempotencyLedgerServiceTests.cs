using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Idempotency;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class IdempotencyLedgerServiceTests
{
    [Fact]
    public async Task FirstRequest_CreatesPendingLedger_WithCanonicalPayloadAndTrimmedScope()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync(
            "  user-1  ",
            "  quiz_answer  ",
            "  operation-1  ",
            "  key-1  ",
            "  /api/quiz/answer  ",
            new { z = 2, a = 1 });

        Assert.False(begin.IsExisting);
        Assert.True(begin.ShouldProcess);
        Assert.True(begin.IsPending);
        Assert.Equal("user-1", begin.Ledger.UserId);
        Assert.Equal("quiz_answer", begin.Ledger.OperationType);
        Assert.Equal("operation-1", begin.Ledger.OperationId);
        Assert.Equal("key-1", begin.Ledger.IdempotencyKey);
        Assert.Equal("/api/quiz/answer", begin.Ledger.Endpoint);

        var persisted = await db.IdempotencyLedgers.SingleAsync();
        Assert.Equal("{\"a\":1,\"z\":2}", persisted.RequestJson);
        Assert.Equal(IdempotencyLedgerStatuses.Pending, persisted.Status);
    }

    [Fact]
    public async Task EquivalentPayloadWithDifferentPropertyOrder_ReplaysCompletedResult()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var first = await service.BeginOrGetExistingAsync(
            "user-1",
            "quiz_answer",
            "operation-1",
            "key-1",
            "/api/quiz/answer",
            new Dictionary<string, object?> { ["b"] = 2, ["a"] = 1 });
        var completed = await service.CompleteAsync(
            first.LedgerId,
            new Dictionary<string, object?> { ["success"] = true, ["score"] = 10 },
            httpStatus: 201);

        var replay = await service.BeginOrGetExistingAsync(
            "user-1",
            "quiz_answer",
            "operation-1",
            "key-1",
            "/api/quiz/answer",
            new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 });

        Assert.True(replay.IsExisting);
        Assert.False(replay.ShouldProcess);
        Assert.True(replay.IsCompleted);
        Assert.Equal(201, replay.Ledger.HttpStatus);
        Assert.Equal(completed.ResultJson, replay.ResultJson);
        Assert.Equal("{\"score\":10,\"success\":true}", replay.ResultJson);
    }

    [Fact]
    public async Task SameKeysWithDifferentPayload_ThrowsConflictWithoutCreatingAnotherLedger()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await service.BeginOrGetExistingAsync(
            "user-1", "quiz_answer", "operation-1", "key-1", "/api/quiz/answer", new { answer = "2" });

        var conflict = await Assert.ThrowsAsync<IdempotencyLedgerConflictException>(() =>
            service.BeginOrGetExistingAsync(
                "user-1", "quiz_answer", "operation-1", "key-1", "/api/quiz/answer", new { answer = "3" }));

        Assert.Equal("user-1", conflict.UserId);
        Assert.Equal("quiz_answer", conflict.OperationType);
        Assert.Equal("operation-1", conflict.OperationId);
        Assert.Equal("key-1", conflict.IdempotencyKey);
        Assert.Equal(1, await db.IdempotencyLedgers.CountAsync());
    }

    [Fact]
    public async Task SameOperationIdWithDifferentIdempotencyKey_ThrowsConflict()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await service.BeginOrGetExistingAsync(
            "user-1", "srs_update", "operation-1", "key-1", "/api/quiz/srs/update", new { quality = 4 });

        await Assert.ThrowsAsync<IdempotencyLedgerConflictException>(() =>
            service.BeginOrGetExistingAsync(
                "user-1", "srs_update", "operation-1", "key-2", "/api/quiz/srs/update", new { quality = 4 }));
    }

    [Fact]
    public async Task SameIdempotencyKeyWithDifferentOperationId_ThrowsConflict()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await service.BeginOrGetExistingAsync(
            "user-1", "srs_update", "operation-1", "key-1", "/api/quiz/srs/update", new { quality = 4 });

        await Assert.ThrowsAsync<IdempotencyLedgerConflictException>(() =>
            service.BeginOrGetExistingAsync(
                "user-1", "srs_update", "operation-2", "key-1", "/api/quiz/srs/update", new { quality = 4 }));
    }

    [Fact]
    public async Task SameKeys_AreIsolatedByUserAndOperationType()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var quizUserOne = await service.BeginOrGetExistingAsync(
            "user-1", "quiz_answer", "operation-1", "key-1", "/api/quiz/answer", new { value = 1 });
        var srsUserOne = await service.BeginOrGetExistingAsync(
            "user-1", "srs_update", "operation-1", "key-1", "/api/quiz/srs/update", new { value = 1 });
        var quizUserTwo = await service.BeginOrGetExistingAsync(
            "user-2", "quiz_answer", "operation-1", "key-1", "/api/quiz/answer", new { value = 1 });

        Assert.True(quizUserOne.ShouldProcess);
        Assert.True(srsUserOne.ShouldProcess);
        Assert.True(quizUserTwo.ShouldProcess);
        Assert.Equal(3, await db.IdempotencyLedgers.CountAsync());
        Assert.Equal(3, new[] { quizUserOne.LedgerId, srsUserOne.LedgerId, quizUserTwo.LedgerId }.Distinct().Count());
    }

    [Fact]
    public async Task FailedLedger_ReplaysStoredFailureWithoutProcessingAgain()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync(
            "user-1", "quiz_answer", "operation-1", "key-1", "/api/quiz/answer", new { answer = "2" });
        var failed = await service.FailAsync(
            begin.LedgerId,
            "question_not_found",
            new { success = false, message = "Question not found" },
            httpStatus: 404);

        var replay = await service.BeginOrGetExistingAsync(
            "user-1", "quiz_answer", "operation-1", "key-1", "/api/quiz/answer", new { answer = "2" });

        Assert.True(failed.IsFailed);
        Assert.True(replay.IsExisting);
        Assert.False(replay.ShouldProcess);
        Assert.True(replay.IsFailed);
        Assert.Equal("question_not_found", replay.ErrorCode);
        Assert.Equal(404, replay.Ledger.HttpStatus);
        Assert.Equal(failed.ResultJson, replay.ResultJson);
    }

    [Fact]
    public async Task Complete_IsIdempotentForEquivalentResult_ButRejectsDifferentResultOrStatus()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var begin = await service.BeginOrGetExistingAsync(
            "user-1", "quiz_answer", "operation-1", "key-1", "/api/quiz/answer", new { answer = "2" });
        var first = await service.CompleteAsync(
            begin.LedgerId,
            new Dictionary<string, object?> { ["success"] = true, ["score"] = 10 },
            httpStatus: 200);
        var same = await service.CompleteAsync(
            begin.LedgerId,
            new Dictionary<string, object?> { ["score"] = 10, ["success"] = true },
            httpStatus: 200);

        Assert.Equal(first.ResultJson, same.ResultJson);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(begin.LedgerId, new { success = true, score = 11 }, httpStatus: 200));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(begin.LedgerId, new { success = true, score = 10 }, httpStatus: 201));
    }

    [Fact]
    public async Task CompletedLedgerCannotFail_AndFailedLedgerCannotComplete()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var completedBegin = await service.BeginOrGetExistingAsync(
            "user-1", "quiz_answer", "operation-completed", "key-completed", "/api/quiz/answer", new { answer = "2" });
        await service.CompleteAsync(completedBegin.LedgerId, new { success = true });

        var failedBegin = await service.BeginOrGetExistingAsync(
            "user-1", "quiz_answer", "operation-failed", "key-failed", "/api/quiz/answer", new { answer = "3" });
        await service.FailAsync(failedBegin.LedgerId, "invalid_answer", new { success = false });

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
            service.BeginOrGetExistingAsync(" ", "quiz_answer", "operation", "key", "/api/quiz/answer", new { }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BeginOrGetExistingAsync("user", " ", "operation", "key", "/api/quiz/answer", new { }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BeginOrGetExistingAsync("user", "quiz_answer", " ", "key", "/api/quiz/answer", new { }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BeginOrGetExistingAsync("user", "quiz_answer", "operation", " ", "/api/quiz/answer", new { }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BeginOrGetExistingAsync("user", "quiz_answer", "operation", "key", " ", new { }));
    }

    private static IdempotencyLedgerService CreateService(ApiDbContext db)
        => new(
            db,
            NullLogger<IdempotencyLedgerService>.Instance,
            new IdempotencyObservabilityService(NullLogger<IdempotencyObservabilityService>.Instance));
}
