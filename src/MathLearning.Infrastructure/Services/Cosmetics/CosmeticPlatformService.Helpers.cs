using System.Text.Json;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public sealed partial class CosmeticPlatformService
{
    private static string GetCatalogCacheKey(string catalogVersion)
        => $"cosmetics:catalog:active:{catalogVersion}";

    private static string GetRewardRulesCacheKey(string sourceType)
        => $"cosmetics:reward-rules:{sourceType}";

    private static string GetAppearanceCacheKey(string userId)
        => $"cosmetics:appearance:{userId}";

    public async Task<CosmeticCatalogImportResultDto> ApplyCatalogManifestAsync(CancellationToken cancellationToken)
        => await ApplyCatalogManifestAsync(CosmeticCatalogManifestProvider.Current, cancellationToken);

    public async Task<CosmeticCatalogImportResultDto> ApplyCatalogManifestAsync(
        CosmeticCatalogManifest manifest,
        CancellationToken cancellationToken)
    {
        var provider = db.Database.ProviderName ?? string.Empty;
        if (!string.Equals(provider, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cosmetic catalog import requires PostgreSQL.");
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            SELECT pg_advisory_xact_lock(hashtext('cosmetic_catalog_import'));
            """, cancellationToken);

        var revisionExists = await db.CosmeticCatalogRevisions
            .AsNoTracking()
            .AnyAsync(x => x.RevisionKey == manifest.RevisionKey, cancellationToken);
        if (revisionExists)
        {
            await transaction.CommitAsync(cancellationToken);
            return new CosmeticCatalogImportResultDto(
                manifest.RevisionKey,
                manifest.Checksum,
                false,
                true,
                await db.CosmeticCatalogRevisions.CountAsync(cancellationToken),
                DateTime.UtcNow);
        }

        await db.Database.ExecuteSqlRawAsync(manifest.UpsertSql, cancellationToken);

        db.CosmeticCatalogRevisions.Add(new CosmeticCatalogRevision
        {
            RevisionKey = manifest.RevisionKey,
            Checksum = manifest.Checksum,
            AppliedBy = "operator_job",
            Notes = "Imported cosmetic catalog manifest",
            AppliedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await InvalidateCatalogCacheAsync(cancellationToken);

        return new CosmeticCatalogImportResultDto(
            manifest.RevisionKey,
            manifest.Checksum,
            true,
            false,
            await db.CosmeticCatalogRevisions.CountAsync(cancellationToken),
            DateTime.UtcNow);
    }

    public async Task<CosmeticCatalogReadinessDto> GetCatalogReadinessAsync(CancellationToken cancellationToken)
    {
        var revision = await db.CosmeticCatalogRevisions
            .AsNoTracking()
            .OrderByDescending(x => x.AppliedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var missingDefaultKeys = new List<string>();
        var fragmentIssues = new List<string>();
        var rewardIssues = new List<string>();
        var installedItemCount = await db.CosmeticItems.CountAsync(cancellationToken);

        if (revision is null)
        {
            return new CosmeticCatalogReadinessDto(
                false,
                "NotReady",
                "CosmeticCatalogRevisionMissing",
                string.Empty,
                string.Empty,
                "catalog-uninitialized",
                installedItemCount,
                [],
                [],
                []);
        }

        foreach (var requiredKey in CosmeticCatalogManifestProvider.Current.RequiredDefaultKeys)
        {
            var item = await db.CosmeticItems.AsNoTracking().FirstOrDefaultAsync(x => x.Key == requiredKey, cancellationToken);
            if (item is null || !item.IsActive || !item.IsDefault)
            {
                missingDefaultKeys.Add(requiredKey);
            }
        }

        foreach (var requiredFragment in CosmeticCatalogManifestProvider.Current.RequiredFragments)
        {
            var matches = await db.CosmeticItems
                .AsNoTracking()
                .Where(x => x.FragmentLabel == requiredFragment.FragmentLabel && x.IsActive)
                .ToListAsync(cancellationToken);

            if (matches.Count != 1)
            {
                fragmentIssues.Add($"{requiredFragment.FragmentLabel}:expected=1,actual={matches.Count}");
                continue;
            }

            var item = matches[0];
            if (!string.Equals(item.Key, requiredFragment.Key, StringComparison.OrdinalIgnoreCase))
            {
                fragmentIssues.Add($"{requiredFragment.FragmentLabel}:key={item.Key}");
            }

            if (item.FragmentsRequired is null || item.FragmentsRequired.Value <= 0)
            {
                fragmentIssues.Add($"{requiredFragment.FragmentLabel}:invalid_required");
            }
        }

        var invalidRewardReferences = await ValidateRewardReferencesAsync(cancellationToken);
        rewardIssues.AddRange(invalidRewardReferences);

        var issues = missingDefaultKeys.Count + fragmentIssues.Count + rewardIssues.Count;
        var isReady = issues == 0;
        var status = isReady ? "Ready" : (missingDefaultKeys.Count > 0 || fragmentIssues.Count > 0 ? "NotReady" : "Degraded");
        var reason = isReady
            ? "CatalogReady"
            : missingDefaultKeys.Count > 0
                ? "CosmeticCatalogDefaultsMissing"
                : fragmentIssues.Count > 0
                    ? "CosmeticCatalogFragmentsInvalid"
                    : "CosmeticCatalogRewardsInvalid";

        return new CosmeticCatalogReadinessDto(
            isReady,
            status,
            reason,
            revision.RevisionKey,
            revision.Checksum,
            revision.RevisionKey,
            installedItemCount,
            missingDefaultKeys,
            fragmentIssues,
            rewardIssues);
    }

    private async Task<List<CosmeticItem>> GetCachedActiveCatalogItemsAsync(string catalogVersion, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            GetCatalogCacheKey(catalogVersion),
            async ct => await db.CosmeticItems
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsHidden)
                .OrderBy(x => x.Category)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToListAsync(ct),
            CatalogCacheTtl,
            CatalogCacheTtl,
            cancellationToken);
    }

    private async Task<List<CosmeticRewardRule>> GetCachedRewardRulesAsync(string sourceType, CancellationToken cancellationToken)
    {
        var normalized = string.IsNullOrWhiteSpace(sourceType) ? "unknown" : sourceType.Trim().ToLowerInvariant();
        return await cache.GetOrCreateAsync(
            GetRewardRulesCacheKey(normalized),
            async ct => await db.CosmeticRewardRules
                .AsNoTracking()
                .Where(x => x.SourceType == normalized && x.IsActive)
                .OrderByDescending(x => x.Priority)
                .ToListAsync(ct),
            RewardRulesCacheTtl,
            RewardRulesCacheTtl,
            cancellationToken);
    }

    private async Task InvalidateCatalogCacheAsync(CancellationToken cancellationToken)
    {
        var version = await BuildCatalogVersionAsync(cancellationToken);
        await cache.RemoveAsync(GetCatalogCacheKey(version), cancellationToken);
    }

    private async Task InvalidateRewardRulesCacheAsync(string sourceType, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(GetRewardRulesCacheKey(sourceType.Trim().ToLowerInvariant()), cancellationToken);
    }

    private async Task InvalidateAppearanceCacheAsync(string userId, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(GetAppearanceCacheKey(userId), cancellationToken);
    }

    private async Task EnsureCatalogReadyForSettlementAsync(CancellationToken cancellationToken)
    {
        var readiness = await GetCatalogReadinessAsync(cancellationToken);
        if (readiness.IsReady)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Cosmetic catalog is not ready: {readiness.Reason}. Revision={readiness.RevisionKey} Checksum={readiness.Checksum}");
    }

    private async Task<UserAvatarConfig> EnsureAvatarConfigAsync(string userId, CancellationToken cancellationToken)
    {
        var config = await db.UserAvatarConfigs.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (config is not null)
        {
            return config;
        }

        config = new UserAvatarConfig
        {
            UserId = userId,
            UpdatedAt = DateTime.UtcNow,
            Version = 0
        };
        db.UserAvatarConfigs.Add(config);
        await db.SaveChangesAsync(cancellationToken);
        return config;
    }

    private async Task EnsureDefaultOwnershipAsync(string userId, CancellationToken cancellationToken)
    {
        var defaultItems = await db.CosmeticItems
            .Where(x => x.IsDefault && x.IsActive)
            .ToListAsync(cancellationToken);
        if (defaultItems.Count == 0)
        {
            return;
        }

        var owned = (await db.UserCosmeticInventories
            .Where(x => x.UserId == userId)
            .Select(x => x.CosmeticItemId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var changed = false;
        foreach (var item in defaultItems)
        {
            if (owned.Contains(item.Id))
            {
                continue;
            }

            db.UserCosmeticInventories.Add(new UserCosmeticInventory
            {
                UserId = userId,
                CosmeticItemId = item.Id,
                Source = CosmeticUnlockTypes.Default,
                SourceRef = $"default:{item.Id}",
                GrantReason = "Default cosmetic ownership",
                SeasonId = item.SeasonId,
                AssetVersion = item.AssetVersion,
                UnlockedAt = DateTime.UtcNow
            });
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ApplyAvatarChangesAsync(
        string userId,
        UserAvatarConfig config,
        IReadOnlyList<EquipCosmeticBatchChangeDto> changes,
        CancellationToken cancellationToken)
    {
        var normalized = changes
            .Where(x => !string.IsNullOrWhiteSpace(x.Slot))
            .Select(x => new EquipCosmeticBatchChangeDto(x.Slot.Trim().ToLowerInvariant(), x.CosmeticItemId))
            .ToList();

        var allowedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CosmeticCategories.Skin,
            CosmeticCategories.Hair,
            CosmeticCategories.Clothing,
            CosmeticCategories.Accessory,
            CosmeticCategories.Emoji,
            CosmeticCategories.Frame,
            CosmeticCategories.Background,
            CosmeticCategories.Effect,
            CosmeticCategories.LeaderboardDecoration
        };

        if (normalized.Any(x => !allowedSlots.Contains(x.Slot)))
        {
            throw new InvalidOperationException("One or more avatar slots are invalid.");
        }

        var requestedIds = normalized.Where(x => x.CosmeticItemId.HasValue).Select(x => x.CosmeticItemId!.Value).Distinct().ToList();
        var itemMap = requestedIds.Count == 0
            ? new Dictionary<int, CosmeticItem>()
            : await db.CosmeticItems
                .Where(x => requestedIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var owned = requestedIds.Count == 0
            ? new HashSet<int>()
            : (await db.UserCosmeticInventories
                .Where(x => x.UserId == userId && requestedIds.Contains(x.CosmeticItemId) && !x.IsRevoked)
                .Select(x => x.CosmeticItemId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        foreach (var change in normalized)
        {
            if (!change.CosmeticItemId.HasValue)
            {
                continue;
            }

            if (!itemMap.TryGetValue(change.CosmeticItemId.Value, out var item))
            {
                throw new InvalidOperationException($"Cosmetic item '{change.CosmeticItemId.Value}' was not found.");
            }

            if (!string.Equals(item.Category, change.Slot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Cosmetic item '{item.Name}' is not valid for slot '{change.Slot}'.");
            }

            if (!item.IsDefault && !owned.Contains(item.Id))
            {
                throw new CosmeticAvatarOwnershipException($"User does not own cosmetic item '{item.Name}'.");
            }
        }

        foreach (var change in normalized)
        {
            SetSlot(config, change.Slot, change.CosmeticItemId);
        }

        config.Version++;
        config.UpdatedAt = DateTime.UtcNow;

        await LogTelemetryAsync(CosmeticTelemetryEventTypes.AvatarUpdated, userId, null, null, new { changes = normalized });
        foreach (var change in normalized)
        {
            await LogTelemetryAsync(
                change.CosmeticItemId.HasValue ? CosmeticTelemetryEventTypes.Equip : CosmeticTelemetryEventTypes.Unequip,
                userId,
                change.CosmeticItemId,
                null,
                new { slot = change.Slot });
        }

        await RebuildAppearanceProjectionAsync(userId, cancellationToken, config);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserAppearanceProjection> RebuildAppearanceProjectionAsync(
        string userId,
        CancellationToken cancellationToken,
        UserAvatarConfig? knownConfig = null)
    {
        var config = knownConfig ?? await db.UserAvatarConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        var projection = await db.UserAppearanceProjections.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (projection is null)
        {
            projection = new UserAppearanceProjection { UserId = userId };
            db.UserAppearanceProjections.Add(projection);
        }

        if (config is null)
        {
            projection.AvatarVersion = 0;
            projection.UpdatedAtUtc = DateTime.UtcNow;
            return projection;
        }

        var ids = BuildEquippedSet(config).ToList();
        var items = ids.Count == 0
            ? new Dictionary<int, CosmeticItem>()
            : await db.CosmeticItems.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);

        projection.AvatarVersion = config.Version;
        projection.SkinId = config.SkinId;
        projection.HairId = config.HairId;
        projection.ClothingId = config.ClothingId;
        projection.AccessoryId = config.AccessoryId;
        projection.EmojiId = config.EmojiId;
        projection.FrameId = config.FrameId;
        projection.BackgroundId = config.BackgroundId;
        projection.EffectId = config.EffectId;
        projection.LeaderboardDecorationId = config.LeaderboardDecorationId;
        projection.SkinAssetPath = ResolveAsset(items, config.SkinId);
        projection.HairAssetPath = ResolveAsset(items, config.HairId);
        projection.ClothingAssetPath = ResolveAsset(items, config.ClothingId);
        projection.AccessoryAssetPath = ResolveAsset(items, config.AccessoryId);
        projection.EmojiAssetPath = ResolveAsset(items, config.EmojiId);
        projection.FrameAssetPath = ResolveAsset(items, config.FrameId);
        projection.BackgroundAssetPath = ResolveAsset(items, config.BackgroundId);
        projection.EffectAssetPath = ResolveAsset(items, config.EffectId);
        projection.LeaderboardDecorationAssetPath = ResolveAsset(items, config.LeaderboardDecorationId);
        projection.UpdatedAtUtc = DateTime.UtcNow;
        await cache.SetAsync(
            GetAppearanceCacheKey(userId),
            projection,
            AppearanceCacheTtl,
            TimeSpan.FromMinutes(2),
            cancellationToken);

        return projection;
    }

    private async Task<CosmeticSeason?> ResolveSeasonAsync(int? seasonId, CancellationToken cancellationToken)
    {
        if (seasonId.HasValue)
        {
            return await db.CosmeticSeasons.AsNoTracking().FirstOrDefaultAsync(x => x.Id == seasonId.Value, cancellationToken);
        }

        var now = DateTime.UtcNow;
        return await db.CosmeticSeasons
            .AsNoTracking()
            .Where(x => x.IsActive && x.StartDate <= now && x.EndDate >= now)
            .OrderByDescending(x => x.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> BuildCatalogVersionAsync(CancellationToken cancellationToken)
    {
        var revision = await db.CosmeticCatalogRevisions
            .AsNoTracking()
            .OrderByDescending(x => x.AppliedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (revision is null)
        {
            return "catalog-uninitialized";
        }

        return revision.RevisionKey;
    }

    private async Task<IReadOnlyList<string>> ValidateRewardReferencesAsync(CancellationToken cancellationToken)
    {
        var issues = new List<string>();

        var rewardRules = await db.CosmeticRewardRules
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.RewardPayloadJson)
            .ToListAsync(cancellationToken);
        foreach (var payloadJson in rewardRules)
        {
            if (!TryParseRewardPayload(payloadJson, out var cosmeticItemId))
            {
                issues.Add("reward_rule:invalid_payload");
                continue;
            }

            var item = await db.CosmeticItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cosmeticItemId && x.IsActive, cancellationToken);
            if (item is null)
            {
                issues.Add($"reward_rule:item_missing:{cosmeticItemId}");
            }
        }

        var seasonRewards = await db.SeasonRewardTrackEntries
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.RewardPayloadJson)
            .ToListAsync(cancellationToken);
        foreach (var payloadJson in seasonRewards)
        {
            if (!TryParseRewardPayload(payloadJson, out var cosmeticItemId))
            {
                issues.Add("reward_track:invalid_payload");
                continue;
            }

            var item = await db.CosmeticItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cosmeticItemId && x.IsActive, cancellationToken);
            if (item is null)
            {
                issues.Add($"reward_track:item_missing:{cosmeticItemId}");
            }
        }

        return issues;
    }

    private static string BuildRewardTrackSourceRef(int seasonId, string trackType, string tierToken)
        => string.IsNullOrWhiteSpace(tierToken)
            ? $"reward-track:{seasonId}:{trackType}:"
            : $"reward-track:{seasonId}:{trackType}:{tierToken}";

    private Task LogTelemetryAsync(
        string eventType,
        string userId,
        int? cosmeticItemId,
        int? seasonId,
        object metadata)
    {
        db.CosmeticTelemetryEvents.Add(new CosmeticTelemetryEvent
        {
            EventType = eventType,
            UserId = userId,
            CosmeticItemId = cosmeticItemId,
            SeasonId = seasonId,
            MetadataJson = JsonSerializer.Serialize(metadata, SerializerOptions),
            OccurredAtUtc = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    private Task WriteAuditAsync(
        string action,
        string? actorUserId,
        string entityType,
        string entityId,
        string? beforeJson,
        string? afterJson)
    {
        db.CosmeticAuditLogs.Add(new CosmeticAuditLog
        {
            Action = action,
            ActorUserId = actorUserId,
            EntityType = entityType,
            EntityId = entityId,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            OccurredAtUtc = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    private static AvatarConfigDto ToAvatarConfigDto(UserAvatarConfig? config)
    {
        return config is null
            ? new AvatarConfigDto(null, null, null, null, null, null, null, null, null, 0)
            : new AvatarConfigDto(
                config.SkinId,
                config.HairId,
                config.ClothingId,
                config.AccessoryId,
                config.EmojiId,
                config.FrameId,
                config.BackgroundId,
                config.EffectId,
                config.LeaderboardDecorationId,
                config.Version);
    }

    private static HashSet<int> BuildEquippedSet(UserAvatarConfig? config)
    {
        var values = new[]
        {
            config?.SkinId,
            config?.HairId,
            config?.ClothingId,
            config?.AccessoryId,
            config?.EmojiId,
            config?.FrameId,
            config?.BackgroundId,
            config?.EffectId,
            config?.LeaderboardDecorationId
        };

        return values.Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
    }

    private static void SetSlot(UserAvatarConfig config, string slot, int? itemId)
    {
        switch (slot)
        {
            case CosmeticCategories.Skin: config.SkinId = itemId; break;
            case CosmeticCategories.Hair: config.HairId = itemId; break;
            case CosmeticCategories.Clothing: config.ClothingId = itemId; break;
            case CosmeticCategories.Accessory: config.AccessoryId = itemId; break;
            case CosmeticCategories.Emoji: config.EmojiId = itemId; break;
            case CosmeticCategories.Frame: config.FrameId = itemId; break;
            case CosmeticCategories.Background: config.BackgroundId = itemId; break;
            case CosmeticCategories.Effect: config.EffectId = itemId; break;
            case CosmeticCategories.LeaderboardDecoration: config.LeaderboardDecorationId = itemId; break;
        }
    }

    private static string? ResolveAsset(IReadOnlyDictionary<int, CosmeticItem> items, int? id)
        => id.HasValue && items.TryGetValue(id.Value, out var item) ? item.AssetPath : null;
}
