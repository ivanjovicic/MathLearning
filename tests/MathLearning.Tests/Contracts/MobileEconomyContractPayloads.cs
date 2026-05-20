namespace MathLearning.Tests.Contracts;

public static class MobileEconomyContractPayloads
{
    public static IReadOnlyDictionary<string, object?> RewardClaimDaily(
        string idempotencyKey,
        string dayKey,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return RewardClaim(
            rewardId: $"daily:{dayKey}",
            rewardType: "daily",
            idempotencyKey: idempotencyKey,
            metadata: metadata);
    }

    public static IReadOnlyDictionary<string, object?> RewardClaimLevel(
        string idempotencyKey,
        int level,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return RewardClaim(
            rewardId: $"level:{level}",
            rewardType: "level",
            idempotencyKey: idempotencyKey,
            metadata: metadata);
    }

    public static IReadOnlyDictionary<string, object?> RewardClaim(
        string rewardId,
        string rewardType,
        string idempotencyKey,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["rewardId"] = rewardId,
            ["rewardType"] = rewardType,
            ["idempotencyKey"] = idempotencyKey
        };

        if (metadata is not null && metadata.Count > 0)
        {
            payload["metadata"] = metadata;
        }

        return payload;
    }

    public static IReadOnlyDictionary<string, object?> CosmeticItemClaim(
        string idempotencyKey,
        string sourceType,
        string? sourceEvent = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = idempotencyKey,
            ["source"] = sourceType,
            ["sourceType"] = sourceType
        };

        if (!string.IsNullOrWhiteSpace(sourceEvent))
        {
            payload["sourceEvent"] = sourceEvent;
        }

        if (metadata is not null && metadata.Count > 0)
        {
            payload["metadata"] = metadata;
        }

        return payload;
    }

    public static IReadOnlyDictionary<string, object?> CosmeticFragmentGrant(
        string idempotencyKey,
        string fragmentName,
        int copies,
        string sourceType = "dailyRun")
    {
        return new Dictionary<string, object?>
        {
            ["idempotencyKey"] = idempotencyKey,
            ["fragmentName"] = fragmentName,
            ["copies"] = copies,
            ["source"] = sourceType
        };
    }

    public static IReadOnlyDictionary<string, object?> SeasonDailyRunClaim(
        string idempotencyKey,
        int seasonId,
        string transactionId,
        int awardedXp)
    {
        return new Dictionary<string, object?>
        {
            ["idempotencyKey"] = idempotencyKey,
            ["seasonId"] = seasonId,
            ["transactionId"] = transactionId,
            ["xp"] = awardedXp
        };
    }

    public static IReadOnlyDictionary<string, object?> SeasonMilestoneClaim(
        string idempotencyKey,
        int seasonId)
    {
        return new Dictionary<string, object?>
        {
            ["idempotencyKey"] = idempotencyKey,
            ["seasonId"] = seasonId
        };
    }
}
