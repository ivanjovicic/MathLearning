using System.Text.Json;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public sealed partial class CosmeticPlatformService
{
    public async Task<IReadOnlyList<CosmeticUnlockResultDto>> ProcessProgressRewardsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null)
        {
            return [];
        }

        var results = new List<CosmeticUnlockResultDto>();
        results.AddRange(await ProcessLegacyItemRewardsAsync(userId, profile, cancellationToken));

        var sources = new[]
        {
            new CosmeticRewardSourceRequest(userId, CosmeticUnlockTypes.Level, $"level:{profile.Level}", JsonSerializer.Serialize(new { level = profile.Level })),
            new CosmeticRewardSourceRequest(userId, CosmeticUnlockTypes.XpMilestone, $"xp:{profile.Xp}", JsonSerializer.Serialize(new { xp = profile.Xp })),
            new CosmeticRewardSourceRequest(userId, CosmeticUnlockTypes.Streak, $"streak:{profile.Streak}", JsonSerializer.Serialize(new { streakDays = profile.Streak }))
        };

        foreach (var source in sources)
        {
            results.AddRange(await ProcessRewardSourceAsync(source, cancellationToken));
        }

        if (results.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return results
            .GroupBy(x => x.CosmeticItemId)
            .Select(x => x.First())
            .OrderBy(x => x.UnlockedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<CosmeticUnlockResultDto>> ProcessRewardSourceAsync(
        CosmeticRewardSourceRequest request,
        CancellationToken cancellationToken)
    {
        var rules = await db.CosmeticRewardRules
            .AsNoTracking()
            .Where(x => x.SourceType == request.SourceType && x.IsActive)
            .OrderByDescending(x => x.Priority)
            .ToListAsync(cancellationToken);

        var results = new List<CosmeticUnlockResultDto>();
        foreach (var rule in rules)
        {
            await LogTelemetryAsync(CosmeticTelemetryEventTypes.RewardEvaluated, request.UserId, null, null, new { request.SourceType, request.SourceRef, rule = rule.Key });

            if (!EvaluateRule(rule, request.PayloadJson))
            {
                continue;
            }

            if (!TryParseRewardPayload(rule.RewardPayloadJson, out var cosmeticItemId))
            {
                continue;
            }

            var item = await db.CosmeticItems.FirstOrDefaultAsync(x => x.Id == cosmeticItemId, cancellationToken);
            if (item is null)
            {
                continue;
            }

            var granted = await TryGrantItemAsync(
                request.UserId,
                item,
                request.SourceType,
                request.SourceRef,
                $"Reward rule {rule.Key}",
                cancellationToken);
            if (granted is not null)
            {
                results.Add(granted);
            }
        }

        return results;
    }

    private async Task<List<CosmeticUnlockResultDto>> ProcessLegacyItemRewardsAsync(string userId, UserProfile profile, CancellationToken cancellationToken)
    {
        var items = await db.CosmeticItems
            .Where(x => x.IsActive && !x.IsHidden && !x.IsDefault)
            .ToListAsync(cancellationToken);

        var results = new List<CosmeticUnlockResultDto>();
        foreach (var item in items)
        {
            if (!EvaluateLegacyUnlock(item, profile, out var sourceRef))
            {
                continue;
            }

            var granted = await TryGrantItemAsync(userId, item, item.UnlockType, sourceRef, $"Legacy unlock {item.UnlockType}", cancellationToken);
            if (granted is not null)
            {
                results.Add(granted);
            }
        }

        return results;
    }

    private async Task<CosmeticUnlockResultDto?> TryGrantItemAsync(
        string userId,
        CosmeticItem item,
        string sourceType,
        string sourceRef,
        string grantReason,
        CancellationToken cancellationToken)
    {
        var rewardKey = $"{sourceType}:{sourceRef}:item:{item.Id}";

        var duplicateClaim = await db.CosmeticRewardClaims
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.RewardKey == rewardKey && x.SourceRef == sourceRef, cancellationToken);
        if (duplicateClaim)
        {
            await LogTelemetryAsync(CosmeticTelemetryEventTypes.RewardClaimDuplicateBlocked, userId, item.Id, item.SeasonId, new { rewardKey, sourceType, sourceRef });
            return null;
        }

        var existingOwnership = await db.UserCosmeticInventories
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.CosmeticItemId == item.Id && !x.IsRevoked, cancellationToken);

        db.CosmeticRewardClaims.Add(new CosmeticRewardClaim
        {
            UserId = userId,
            RewardKey = rewardKey,
            SourceType = sourceType,
            SourceRef = sourceRef,
            CosmeticItemId = item.Id,
            ClaimedAtUtc = DateTime.UtcNow
        });

        if (existingOwnership)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        db.UserCosmeticInventories.Add(new UserCosmeticInventory
        {
            UserId = userId,
            CosmeticItemId = item.Id,
            Source = sourceType,
            SourceRef = sourceRef,
            GrantReason = grantReason,
            SeasonId = item.SeasonId,
            AssetVersion = item.AssetVersion,
            UnlockedAt = now
        });

        await LogTelemetryAsync(CosmeticTelemetryEventTypes.Unlock, userId, item.Id, item.SeasonId, new { sourceType, sourceRef, rewardKey });
        logger.LogInformation(
            "Cosmetic unlocked. UserId={UserId} CosmeticItemId={CosmeticItemId} SourceType={SourceType} SourceRef={SourceRef}",
            userId,
            item.Id,
            sourceType,
            sourceRef);

        return new CosmeticUnlockResultDto(item.Id, item.Key, item.Name, item.Category, item.Rarity, sourceType, sourceRef, now);
    }

    private static bool EvaluateLegacyUnlock(CosmeticItem item, UserProfile profile, out string sourceRef)
    {
        sourceRef = $"{item.UnlockType}:{item.Id}";
        if (string.IsNullOrWhiteSpace(item.UnlockCondition))
        {
            return false;
        }

        var condition = item.UnlockCondition.Trim().ToLowerInvariant();
        if (item.UnlockType == CosmeticUnlockTypes.Level && TryParseThreshold(condition, "level", out var level))
        {
            sourceRef = $"level:{level}";
            return profile.Level >= level;
        }

        if (item.UnlockType == CosmeticUnlockTypes.XpMilestone && TryParseThreshold(condition, "xp", out var xp))
        {
            sourceRef = $"xp:{xp}";
            return profile.Xp >= xp;
        }

        if (item.UnlockType == CosmeticUnlockTypes.Streak && TryParseThreshold(condition, "streak", out var streak))
        {
            sourceRef = $"streak:{streak}";
            return profile.Streak >= streak;
        }

        return false;
    }

    private static bool TryParseThreshold(string value, string prefix, out int threshold)
    {
        threshold = 0;
        if (!value.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(value[(prefix.Length + 1)..], out threshold);
    }

    private static bool EvaluateRule(CosmeticRewardRule rule, string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(rule.ConditionJson))
        {
            return true;
        }

        try
        {
            using var payloadDoc = string.IsNullOrWhiteSpace(payloadJson) ? JsonDocument.Parse("{}") : JsonDocument.Parse(payloadJson);
            using var conditionDoc = JsonDocument.Parse(rule.ConditionJson);
            var payload = payloadDoc.RootElement;
            var condition = conditionDoc.RootElement;

            if (condition.TryGetProperty("level", out var levelCondition) &&
                payload.TryGetProperty("level", out var payloadLevel) &&
                payloadLevel.GetInt32() < levelCondition.GetInt32())
            {
                return false;
            }

            if (condition.TryGetProperty("minXp", out var minXpCondition) &&
                payload.TryGetProperty("xp", out var payloadXp) &&
                payloadXp.GetInt32() < minXpCondition.GetInt32())
            {
                return false;
            }

            if (condition.TryGetProperty("streakDays", out var streakCondition) &&
                payload.TryGetProperty("streakDays", out var payloadStreak) &&
                payloadStreak.GetInt32() < streakCondition.GetInt32())
            {
                return false;
            }

            if (condition.TryGetProperty("badgeKey", out var badgeCondition))
            {
                return payload.TryGetProperty("badgeKey", out var payloadBadge) &&
                       string.Equals(payloadBadge.GetString(), badgeCondition.GetString(), StringComparison.OrdinalIgnoreCase);
            }

            if (condition.TryGetProperty("maxRank", out var maxRankCondition))
            {
                return payload.TryGetProperty("rank", out var payloadRank) &&
                       payloadRank.GetInt32() <= maxRankCondition.GetInt32();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseRewardPayload(string rewardPayloadJson, out int cosmeticItemId)
    {
        cosmeticItemId = 0;
        try
        {
            using var doc = JsonDocument.Parse(rewardPayloadJson);
            return doc.RootElement.TryGetProperty("cosmeticItemId", out var itemId) &&
                   itemId.TryGetInt32(out cosmeticItemId);
        }
        catch
        {
            return false;
        }
    }
}
