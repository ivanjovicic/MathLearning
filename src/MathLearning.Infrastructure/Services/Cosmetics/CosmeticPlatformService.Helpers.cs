using System.Text.Json;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public sealed partial class CosmeticPlatformService
{
    private static string GetCatalogCacheKey()
        => "cosmetics:catalog:active";

    private static string GetCatalogVersionCacheKey()
        => "cosmetics:catalog:version";

    private static string GetRewardRulesCacheKey(string sourceType)
        => $"cosmetics:reward-rules:{sourceType}";

    private static string GetAppearanceCacheKey(string userId)
        => $"cosmetics:appearance:{userId}";

    private async Task<List<CosmeticItem>> GetCachedActiveCatalogItemsAsync(CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            GetCatalogCacheKey(),
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
        await cache.RemoveAsync(GetCatalogCacheKey(), cancellationToken);
        await cache.RemoveAsync(GetCatalogVersionCacheKey(), cancellationToken);
    }

    private async Task InvalidateRewardRulesCacheAsync(string sourceType, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(GetRewardRulesCacheKey(sourceType.Trim().ToLowerInvariant()), cancellationToken);
    }

    private async Task InvalidateAppearanceCacheAsync(string userId, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(GetAppearanceCacheKey(userId), cancellationToken);
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
                throw new InvalidOperationException($"User does not own cosmetic item '{item.Name}'.");
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
        return await cache.GetOrCreateAsync(
            GetCatalogVersionCacheKey(),
            async ct =>
            {
                var stamp = await db.CosmeticItems
                    .AsNoTracking()
                    .OrderByDescending(x => x.UpdatedAt)
                    .Select(x => (DateTime?)x.UpdatedAt)
                    .FirstOrDefaultAsync(ct);

                return $"catalog-{(stamp ?? DateTime.UtcNow):yyyyMMddHHmmss}";
            },
            CatalogCacheTtl,
            CatalogCacheTtl,
            cancellationToken);
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
