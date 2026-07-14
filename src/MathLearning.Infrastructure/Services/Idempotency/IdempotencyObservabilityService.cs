using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.Idempotency;

public sealed class IdempotencyObservabilityService
{
    private const int SuffixLength = 8;

    private readonly ConcurrentDictionary<string, long> counters = new(StringComparer.Ordinal);
    private readonly ILogger<IdempotencyObservabilityService> logger;

    public IdempotencyObservabilityService(ILogger<IdempotencyObservabilityService> logger)
    {
        this.logger = logger;
    }

    public void RecordFirstSuccess(string endpoint, string operationType, string operationId, string userId)
        => Record("first_success", endpoint, operationType, operationId, userId, "completed", errorCode: null, logLevel: null);

    public void RecordReplay(string endpoint, string operationType, string operationId, string userId, string status)
        => Record("replay", endpoint, operationType, operationId, userId, status, errorCode: null, logLevel: LogLevel.Information);

    public void RecordConflict(string endpoint, string operationType, string operationId, string userId)
        => Record("conflict", endpoint, operationType, operationId, userId, "conflict", errorCode: null, logLevel: LogLevel.Warning);

    public void RecordFailure(
        string endpoint,
        string operationType,
        string operationId,
        string userId,
        string status,
        string? errorCode = null)
        => Record("failure", endpoint, operationType, operationId, userId, status, errorCode, LogLevel.Warning);

    public void RecordRollback(
        string endpoint,
        string operationType,
        string operationId,
        string userId,
        string? errorCode = null)
        => Record("rollback", endpoint, operationType, operationId, userId, "rolled_back", errorCode, LogLevel.Warning);

    public IdempotencyObservabilitySnapshot Snapshot()
    {
        var rows = counters
            .Select(static entry =>
            {
                var parts = entry.Key.Split('|');
                return new IdempotencyObservabilityRow(
                    Category: parts[0],
                    Endpoint: parts[1],
                    OperationType: parts[2],
                    Status: parts[3],
                    Count: entry.Value);
            })
            .OrderBy(x => x.Category, StringComparer.Ordinal)
            .ThenBy(x => x.Endpoint, StringComparer.Ordinal)
            .ThenBy(x => x.OperationType, StringComparer.Ordinal)
            .ThenBy(x => x.Status, StringComparer.Ordinal)
            .ToArray();

        return new IdempotencyObservabilitySnapshot(
            FirstSuccess: SumByCategory("first_success"),
            Replay: SumByCategory("replay"),
            Conflict: SumByCategory("conflict"),
            Failure: SumByCategory("failure"),
            Rollback: SumByCategory("rollback"),
            Rows: rows);
    }

    public void Reset() => counters.Clear();

    public static string ResolveEconomyEndpoint(string transactionType)
    {
        return transactionType switch
        {
            "economy_coins_spend" => "POST /api/economy/coins/spend",
            "economy_hint_use" => "POST /api/economy/hints/use",
            "economy_reward_claim" => "POST /api/economy/rewards/claim",
            "shop_streak_freeze_purchase" => "POST /api/shop/streak-freeze/purchase",
            "admin_economy_reward_grant" => "POST /api/admin/economy/rewards/grant",
            "season_daily_run_claim" => "POST /api/seasons/daily-run-claim",
            "season_milestone_claim" => "POST /api/seasons/milestones/{milestoneId}/claim",
            _ => $"transaction:{transactionType}"
        };
    }

    public static string ResolveCosmeticsEndpoint(string operationType)
    {
        return operationType switch
        {
            "cosmetics_item_claim" => "POST /api/cosmetics/items/{itemKey}/claim",
            "cosmetics_fragment_grant" => "POST /api/cosmetics/fragments/grant",
            "cosmetics_shop_purchase" => "POST /api/cosmetics/purchase",
            _ => $"operation:{operationType}"
        };
    }

    private void Record(
        string category,
        string endpoint,
        string operationType,
        string operationId,
        string userId,
        string status,
        string? errorCode,
        LogLevel? logLevel)
    {
        var effectiveEndpoint = Normalize(endpoint);
        var effectiveOperationType = Normalize(operationType);
        var effectiveStatus = Normalize(status);
        var key = $"{category}|{effectiveEndpoint}|{effectiveOperationType}|{effectiveStatus}";
        counters.AddOrUpdate(key, 1, static (_, value) => value + 1);

        if (logLevel is null)
            return;

        logger.Log(
            logLevel.Value,
            "Idempotency {Category}. Endpoint={Endpoint} OperationType={OperationType} OperationSuffix={OperationSuffix} UserHash={UserHash} Status={Status} ErrorCode={ErrorCode}",
            category,
            effectiveEndpoint,
            effectiveOperationType,
            ShortSuffix(operationId),
            HashUserId(userId),
            effectiveStatus,
            errorCode);
    }

    private long SumByCategory(string category)
        => counters.Where(x => x.Key.StartsWith(category + "|", StringComparison.Ordinal)).Sum(x => x.Value);

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();

    private static string ShortSuffix(string? value)
    {
        var normalized = Normalize(value);
        return normalized.Length <= SuffixLength
            ? normalized
            : normalized[^SuffixLength..];
    }

    private static string HashUserId(string? userId)
    {
        var normalized = Normalize(userId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash[..6]);
    }
}

public sealed record IdempotencyObservabilitySnapshot(
    long FirstSuccess,
    long Replay,
    long Conflict,
    long Failure,
    long Rollback,
    IReadOnlyList<IdempotencyObservabilityRow> Rows);

public sealed record IdempotencyObservabilityRow(
    string Category,
    string Endpoint,
    string OperationType,
    string Status,
    long Count);
