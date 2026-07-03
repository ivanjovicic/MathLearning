using System.Collections.Concurrent;
using MathLearning.Infrastructure.Services.Idempotency;
using Microsoft.Extensions.Logging;

namespace MathLearning.Tests.Services;

public sealed class IdempotencyObservabilityServiceTests
{
    [Fact]
    public void NewService_HasEmptySnapshot()
    {
        var service = CreateService();

        var snapshot = service.Snapshot();

        Assert.Equal(0L, snapshot.FirstSuccess);
        Assert.Equal(0L, snapshot.Replay);
        Assert.Equal(0L, snapshot.Conflict);
        Assert.Equal(0L, snapshot.Failure);
        Assert.Equal(0L, snapshot.Rollback);
        Assert.Empty(snapshot.Rows);
    }

    [Fact]
    public void RecordMethods_AggregateCategoryTotalsAndNormalizedRows()
    {
        var service = CreateService();

        service.RecordFirstSuccess(" POST /api/quiz/answer ", " quiz_answer ", "op-1", "user-1");
        service.RecordFirstSuccess("POST /api/quiz/answer", "quiz_answer", "op-2", "user-2");
        service.RecordReplay("POST /api/quiz/answer", "quiz_answer", "op-1", "user-1", " completed ");
        service.RecordConflict("POST /api/quiz/answer", "quiz_answer", "op-3", "user-1");
        service.RecordFailure(" ", " ", "op-4", "user-1", " ", "SAFE_CODE");
        service.RecordRollback("POST /api/quiz/answer", "quiz_answer", "op-5", "user-1", "SAFE_CODE");

        var snapshot = service.Snapshot();

        Assert.Equal(2L, snapshot.FirstSuccess);
        Assert.Equal(1L, snapshot.Replay);
        Assert.Equal(1L, snapshot.Conflict);
        Assert.Equal(1L, snapshot.Failure);
        Assert.Equal(1L, snapshot.Rollback);

        Assert.Contains(snapshot.Rows, row =>
            row.Category == "first_success" &&
            row.Endpoint == "POST /api/quiz/answer" &&
            row.OperationType == "quiz_answer" &&
            row.Status == "completed" &&
            row.Count == 2L);
        Assert.Contains(snapshot.Rows, row =>
            row.Category == "failure" &&
            row.Endpoint == "unknown" &&
            row.OperationType == "unknown" &&
            row.Status == "unknown" &&
            row.Count == 1L);

        var expectedOrder = snapshot.Rows
            .OrderBy(row => row.Category, StringComparer.Ordinal)
            .ThenBy(row => row.Endpoint, StringComparer.Ordinal)
            .ThenBy(row => row.OperationType, StringComparer.Ordinal)
            .ThenBy(row => row.Status, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedOrder, snapshot.Rows.ToArray());
    }

    [Fact]
    public void ConcurrentRecording_DoesNotLoseIncrements()
    {
        var service = CreateService();
        const int count = 20_000;

        Parallel.For(0, count, index =>
            service.RecordFirstSuccess(
                endpoint: index % 2 == 0 ? "endpoint-a" : "endpoint-b",
                operationType: "quiz_answer",
                operationId: $"operation-{index}",
                userId: $"user-{index}"));

        var snapshot = service.Snapshot();

        Assert.Equal((long)count, snapshot.FirstSuccess);
        Assert.Equal(2, snapshot.Rows.Count);
        Assert.Equal((long)(count / 2), snapshot.Rows.Single(row => row.Endpoint == "endpoint-a").Count);
        Assert.Equal((long)(count / 2), snapshot.Rows.Single(row => row.Endpoint == "endpoint-b").Count);
    }

    [Fact]
    public void Reset_ClearsAllCountersAndRows()
    {
        var service = CreateService();
        service.RecordFirstSuccess("endpoint", "operation", "operation-id", "user-id");
        service.RecordReplay("endpoint", "operation", "operation-id", "user-id", "completed");

        service.Reset();
        var snapshot = service.Snapshot();

        Assert.Equal(0L, snapshot.FirstSuccess);
        Assert.Equal(0L, snapshot.Replay);
        Assert.Empty(snapshot.Rows);
    }

    [Fact]
    public void LoggedReplay_UsesOperationSuffixAndUserHashInsteadOfRawIdentifiers()
    {
        var logger = new RecordingLogger<IdempotencyObservabilityService>();
        var service = new IdempotencyObservabilityService(logger);
        const string operationId = "private-operation-1234567890";
        const string userId = "private-user@example.test";

        service.RecordReplay(
            "POST /api/quiz/answer",
            "quiz_answer",
            operationId,
            userId,
            "completed");

        var message = Assert.Single(logger.Messages);
        Assert.DoesNotContain(operationId, message, StringComparison.Ordinal);
        Assert.DoesNotContain(userId, message, StringComparison.Ordinal);
        Assert.DoesNotContain("private-user", message, StringComparison.Ordinal);
        Assert.Contains("34567890", message, StringComparison.Ordinal);
        Assert.Contains("POST /api/quiz/answer", message, StringComparison.Ordinal);
        Assert.Contains("quiz_answer", message, StringComparison.Ordinal);
        Assert.Contains("completed", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("economy_coins_spend", "POST /api/economy/coins/spend")]
    [InlineData("economy_hint_use", "POST /api/economy/hints/use")]
    [InlineData("economy_reward_claim", "POST /api/economy/rewards/claim")]
    [InlineData("shop_streak_freeze_purchase", "POST /api/shop/streak-freeze/purchase")]
    [InlineData("admin_economy_reward_grant", "POST /api/admin/economy/rewards/grant")]
    [InlineData("season_daily_run_claim", "POST /api/seasons/daily-run-claim")]
    [InlineData("season_milestone_claim", "POST /api/seasons/milestones/{milestoneId}/claim")]
    public void ResolveEconomyEndpoint_KnownTypeReturnsCanonicalRoute(string operationType, string expected)
    {
        Assert.Equal(expected, IdempotencyObservabilityService.ResolveEconomyEndpoint(operationType));
    }

    [Fact]
    public void ResolveEconomyEndpoint_UnknownTypeUsesBoundedDiagnosticFallback()
    {
        Assert.Equal(
            "transaction:future_transaction",
            IdempotencyObservabilityService.ResolveEconomyEndpoint("future_transaction"));
    }

    [Theory]
    [InlineData("cosmetics_item_claim", "POST /api/cosmetics/items/{itemKey}/claim")]
    [InlineData("cosmetics_fragment_grant", "POST /api/cosmetics/fragments/grant")]
    public void ResolveCosmeticsEndpoint_KnownTypeReturnsCanonicalRoute(string operationType, string expected)
    {
        Assert.Equal(expected, IdempotencyObservabilityService.ResolveCosmeticsEndpoint(operationType));
    }

    [Fact]
    public void ResolveCosmeticsEndpoint_UnknownTypeUsesDiagnosticFallback()
    {
        Assert.Equal(
            "operation:future_cosmetic_operation",
            IdempotencyObservabilityService.ResolveCosmeticsEndpoint("future_cosmetic_operation"));
    }

    private static IdempotencyObservabilityService CreateService() =>
        new(new RecordingLogger<IdempotencyObservabilityService>());

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<string> messages = new();

        public IReadOnlyList<string> Messages => messages.ToArray();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
