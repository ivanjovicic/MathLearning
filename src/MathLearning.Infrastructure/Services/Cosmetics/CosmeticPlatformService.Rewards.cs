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
            results.AddRange(await ProcessRewardSourceInternalAsync(source, saveChanges: false, cancellationToken));
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return results
            .GroupBy(x => x.CosmeticItemId)
            .Select(x => x.First())
            .OrderBy(x => x.UnlockedAtUtc)
            .ToList();
    }

    public Task<IReadOnlyList<CosmeticUnlockResultDto>> ProcessRewardSourceAsync(
        CosmeticRewardSourceRequest request,
        CancellationToken cancellationToken)
        => ProcessRewardSourceInternalAsync(request, saveChanges: true, cancellationToken);

    private async Task<IReadOnlyList<CosmeticUnlockResultDto>> ProcessRewardSourceInternalAsync(
        CosmeticRewardSourceRequest request,
        bool saveChanges,
        CancellationToken cancellationToken)
    {
        var rules = await GetCachedRewardRulesAsync(request.SourceType, cancellationToken);
        var results = new List<CosmeticUnlockResultDto>();

        var legacyCandidates = await GetLegacySourceRewardCandidatesAsync(request, cancellationToken);
        var ruleCandidates = new List<(CosmeticRewardRule Rule, int CosmeticItemId)>();

        foreach (var rule in rules)
        {
            await LogTelemetryAsync(
                CosmeticTelemetryEventTypes.RewardEvaluated,
                request.UserId,
                null,
                null,
                new { request.SourceType, request.SourceRef, rule = rule.Key });

            if (!EvaluateRule(rule, request.PayloadJson) || !TryParseRewardPayload(rule.RewardPayloadJson, out var cosmeticItemId))
            {
                continue;
            }

            ruleCandidates.Add((rule, cosmeticItemId));
        }

        var candidateItemIds = legacyCandidates
            .Select(x => x.Id)
            .Concat(ruleCandidates.Select(x => x.CosmeticItemId))
            .Distinct()
            .ToList();

        var itemsById = candidateItemIds.Count == 0
            ? new Dictionary<int, CosmeticItem>()
            : (await db.CosmeticItems
                .AsNoTracking()
                .Where(x => candidateItemIds.Contains(x.Id))
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.Id);
        var grantContext = await LoadRewardGrantContextAsync(request.UserId, request.SourceType, request.SourceRef, candidateItemIds, cancellationToken);

        foreach (var item in legacyCandidates)
        {
            var granted = await TryGrantItemAsync(
                request.UserId,
                item,
                request.SourceType,
                request.SourceRef,
                $"Legacy source unlock {request.SourceType}",
                grantContext,
                cancellationToken);
            if (granted is not null)
            {
                results.Add(granted);
            }
        }

        foreach (var candidate in ruleCandidates)
        {
            if (!itemsById.TryGetValue(candidate.CosmeticItemId, out var item))
            {
                continue;
            }

            var granted = await TryGrantItemAsync(
                request.UserId,
                item,
                request.SourceType,
                request.SourceRef,
                $"Reward rule {candidate.Rule.Key}",
                grantContext,
                cancellationToken);
            if (granted is not null)
            {
                results.Add(granted);
            }
        }

        if (saveChanges && db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return results;
    }

    public async Task<ClaimRewardTrackTierResponse> ClaimRewardTrackTierAsync(
        string userId,
        ClaimRewardTrackTierRequest request,
        CancellationToken cancellationToken)
    {
        var effectiveTrackType = string.IsNullOrWhiteSpace(request.TrackType)
            ? CosmeticTrackTypes.Free
            : request.TrackType.Trim().ToLowerInvariant();
        var season = await ResolveSeasonAsync(request.SeasonId, cancellationToken)
            ?? throw new InvalidOperationException("Season not found.");

        var entry = await db.SeasonRewardTrackEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.SeasonId == season.Id &&
                     x.TrackType == effectiveTrackType &&
                     x.Tier == request.Tier &&
                     x.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException("Reward track tier not found.");

        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Profile not found.");
        if (profile.Xp < entry.XpRequired)
        {
            throw new InvalidOperationException("Reward track tier is not unlocked yet.");
        }

        var sourceRef = BuildRewardTrackSourceRef(season.Id, effectiveTrackType, entry.Tier.ToString());
        if (!TryParseRewardPayload(entry.RewardPayloadJson, out var cosmeticItemId))
        {
            throw new InvalidOperationException("Reward track payload is invalid.");
        }

        var item = await db.CosmeticItems.FirstOrDefaultAsync(x => x.Id == cosmeticItemId, cancellationToken)
            ?? throw new InvalidOperationException("Reward track cosmetic item was not found.");
        var grantContext = await LoadRewardGrantContextAsync(
            userId,
            CosmeticUnlockTypes.RewardTrack,
            sourceRef,
            [item.Id],
            cancellationToken);

        if (grantContext.ClaimedRewardKeys.Contains(BuildRewardKey(CosmeticUnlockTypes.RewardTrack, sourceRef, item.Id)))
        {
            return new ClaimRewardTrackTierResponse(true, true, season.Id, effectiveTrackType, entry.Tier, []);
        }

        var reward = await TryGrantItemAsync(
            userId,
            item,
            CosmeticUnlockTypes.RewardTrack,
            sourceRef,
            $"Reward track {effectiveTrackType} tier {entry.Tier}",
            grantContext,
            cancellationToken);

        await LogTelemetryAsync(
            CosmeticTelemetryEventTypes.RewardTrackClaimed,
            userId,
            item.Id,
            season.Id,
            new { seasonId = season.Id, trackType = effectiveTrackType, tier = entry.Tier });
        await db.SaveChangesAsync(cancellationToken);

        return new ClaimRewardTrackTierResponse(
            true,
            false,
            season.Id,
            effectiveTrackType,
            entry.Tier,
            reward is null ? [] : [reward]);
    }

    private async Task<List<CosmeticUnlockResultDto>> ProcessLegacyItemRewardsAsync(string userId, UserProfile profile, CancellationToken cancellationToken)
    {
        var items = (await GetCachedActiveCatalogItemsAsync(cancellationToken))
            .Where(x => !x.IsDefault)
            .ToList();

        var eligibleItems = new List<(CosmeticItem Item, string SourceRef)>();
        foreach (var item in items)
        {
            if (!EvaluateLegacyUnlock(item, profile, out var sourceRef))
            {
                continue;
            }

            eligibleItems.Add((item, sourceRef));
        }

        if (eligibleItems.Count == 0)
        {
            return [];
        }

        var results = new List<CosmeticUnlockResultDto>(eligibleItems.Count);

        foreach (var group in eligibleItems.GroupBy(x => (x.Item.UnlockType, x.SourceRef)))
        {
            var itemIds = group.Select(x => x.Item.Id).Distinct().ToList();
            var context = await LoadRewardGrantContextAsync(userId, group.Key.UnlockType, group.Key.SourceRef, itemIds, cancellationToken);

            foreach (var candidate in group)
            {
                var granted = await TryGrantItemAsync(
                    userId,
                    candidate.Item,
                    candidate.Item.UnlockType,
                    candidate.SourceRef,
                    $"Legacy unlock {candidate.Item.UnlockType}",
                    context,
                    cancellationToken);
                if (granted is not null)
                {
                    results.Add(granted);
                }
            }
        }

        return results;
    }

    private async Task<List<CosmeticItem>> GetLegacySourceRewardCandidatesAsync(
        CosmeticRewardSourceRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return (await GetCachedActiveCatalogItemsAsync(cancellationToken))
            .Where(x =>
                x.UnlockType == request.SourceType &&
                (x.ReleaseDate == null || x.ReleaseDate <= now) &&
                (x.RetirementDate == null || x.RetirementDate > now) &&
                EvaluateLegacySourceUnlock(x, request.PayloadJson))
            .ToList();
    }

    private async Task<CosmeticUnlockResultDto?> TryGrantItemAsync(
        string userId,
        CosmeticItem item,
        string sourceType,
        string sourceRef,
        string grantReason,
        CancellationToken cancellationToken)
    {
        var context = await LoadRewardGrantContextAsync(userId, sourceType, sourceRef, [item.Id], cancellationToken);
        return await TryGrantItemAsync(userId, item, sourceType, sourceRef, grantReason, context, cancellationToken);
    }

    private async Task<CosmeticUnlockResultDto?> TryGrantItemAsync(
        string userId,
        CosmeticItem item,
        string sourceType,
        string sourceRef,
        string grantReason,
        RewardGrantContext context,
        CancellationToken cancellationToken)
    {
        var rewardKey = BuildRewardKey(sourceType, sourceRef, item.Id);
        if (!context.ClaimedRewardKeys.Add(rewardKey))
        {
            await LogTelemetryAsync(
                CosmeticTelemetryEventTypes.RewardClaimDuplicateBlocked,
                userId,
                item.Id,
                item.SeasonId,
                new { rewardKey, sourceType, sourceRef });
            return null;
        }

        db.CosmeticRewardClaims.Add(new CosmeticRewardClaim
        {
            UserId = userId,
            RewardKey = rewardKey,
            SourceType = sourceType,
            SourceRef = sourceRef,
            CosmeticItemId = item.Id,
            ClaimedAtUtc = DateTime.UtcNow
        });

        if (!context.OwnedItemIds.Add(item.Id))
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

    private async Task<RewardGrantContext> LoadRewardGrantContextAsync(
        string userId,
        string sourceType,
        string sourceRef,
        IReadOnlyCollection<int> itemIds,
        CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0)
        {
            return new RewardGrantContext(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<int>());
        }

        var normalizedSourceType = string.IsNullOrWhiteSpace(sourceType) ? string.Empty : sourceType.Trim().ToLowerInvariant();
        var normalizedSourceRef = sourceRef?.Trim() ?? string.Empty;

        var claimedRewardKeys = (await db.CosmeticRewardClaims
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.SourceType == normalizedSourceType &&
                x.SourceRef == normalizedSourceRef &&
                itemIds.Contains(x.CosmeticItemId))
            .Select(x => x.RewardKey)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ownedItemIds = (await db.UserCosmeticInventories
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsRevoked && itemIds.Contains(x.CosmeticItemId))
            .Select(x => x.CosmeticItemId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        return new RewardGrantContext(claimedRewardKeys, ownedItemIds);
    }

    private static string BuildRewardKey(string sourceType, string sourceRef, int itemId)
        => $"{sourceType}:{sourceRef}:item:{itemId}";

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
            var matches = true;

            if (condition.TryGetProperty("level", out var levelCondition) &&
                payload.TryGetProperty("level", out var payloadLevel) &&
                payloadLevel.GetInt32() < levelCondition.GetInt32())
            {
                matches = false;
            }

            if (condition.TryGetProperty("minXp", out var minXpCondition) &&
                payload.TryGetProperty("xp", out var payloadXp) &&
                payloadXp.GetInt32() < minXpCondition.GetInt32())
            {
                matches = false;
            }

            if (condition.TryGetProperty("streakDays", out var streakCondition) &&
                payload.TryGetProperty("streakDays", out var payloadStreak) &&
                payloadStreak.GetInt32() < streakCondition.GetInt32())
            {
                matches = false;
            }

            if (condition.TryGetProperty("badgeKey", out var badgeCondition))
            {
                matches = matches &&
                          payload.TryGetProperty("badgeKey", out var payloadBadge) &&
                          string.Equals(payloadBadge.GetString(), badgeCondition.GetString(), StringComparison.OrdinalIgnoreCase);
            }

            if (condition.TryGetProperty("scope", out var scopeCondition))
            {
                matches = matches &&
                          payload.TryGetProperty("scope", out var payloadScope) &&
                          string.Equals(payloadScope.GetString(), scopeCondition.GetString(), StringComparison.OrdinalIgnoreCase);
            }

            if (condition.TryGetProperty("period", out var periodCondition))
            {
                matches = matches &&
                          payload.TryGetProperty("period", out var payloadPeriod) &&
                          string.Equals(payloadPeriod.GetString(), periodCondition.GetString(), StringComparison.OrdinalIgnoreCase);
            }

            if (condition.TryGetProperty("trackType", out var trackTypeCondition))
            {
                matches = matches &&
                          payload.TryGetProperty("trackType", out var payloadTrackType) &&
                          string.Equals(payloadTrackType.GetString(), trackTypeCondition.GetString(), StringComparison.OrdinalIgnoreCase);
            }

            if (condition.TryGetProperty("maxRank", out var maxRankCondition))
            {
                matches = matches &&
                          payload.TryGetProperty("rank", out var payloadRank) &&
                          payloadRank.GetInt32() <= maxRankCondition.GetInt32();
            }

            if (condition.TryGetProperty("maxPlacement", out var maxPlacementCondition))
            {
                matches = matches &&
                          payload.TryGetProperty("placement", out var payloadPlacement) &&
                          payloadPlacement.GetInt32() <= maxPlacementCondition.GetInt32();
            }

            if (condition.TryGetProperty("tier", out var tierCondition))
            {
                matches = matches &&
                          payload.TryGetProperty("tier", out var payloadTier) &&
                          payloadTier.GetInt32() == tierCondition.GetInt32();
            }

            return matches;
        }
        catch
        {
            return false;
        }
    }

    private static bool EvaluateLegacySourceUnlock(CosmeticItem item, string? payloadJson)
    {
        try
        {
            using var payloadDoc = string.IsNullOrWhiteSpace(payloadJson) ? JsonDocument.Parse("{}") : JsonDocument.Parse(payloadJson);
            var payload = payloadDoc.RootElement;
            var condition = item.UnlockCondition?.Trim();

            return item.UnlockType switch
            {
                CosmeticUnlockTypes.Badge => payload.TryGetProperty("badgeKey", out var badgeKey) &&
                    !string.IsNullOrWhiteSpace(condition) &&
                    string.Equals(badgeKey.GetString(), condition, StringComparison.OrdinalIgnoreCase),
                CosmeticUnlockTypes.Leaderboard => MatchesRankCondition(condition, payload, "rank"),
                CosmeticUnlockTypes.SchoolCompetition => MatchesRankCondition(condition, payload, "placement"),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesRankCondition(string? condition, JsonElement payload, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(condition) ||
            !payload.TryGetProperty(fieldName, out var rankElement) ||
            !rankElement.TryGetInt32(out var rank))
        {
            return false;
        }

        var normalized = condition.Trim().ToLowerInvariant();
        if (normalized.StartsWith("top:", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(normalized["top:".Length..], out var threshold))
        {
            return rank <= threshold;
        }

        if (normalized.StartsWith("rank:", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(normalized["rank:".Length..], out var exactRank))
        {
            return rank == exactRank;
        }

        return false;
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

    private sealed class RewardGrantContext
    {
        public RewardGrantContext(HashSet<string> claimedRewardKeys, HashSet<int> ownedItemIds)
        {
            ClaimedRewardKeys = claimedRewardKeys;
            OwnedItemIds = ownedItemIds;
        }

        public HashSet<string> ClaimedRewardKeys { get; }
        public HashSet<int> OwnedItemIds { get; }
    }
}
