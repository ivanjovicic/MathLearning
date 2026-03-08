using System.Text.Json;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public sealed partial class CosmeticPlatformService
{
    public async Task<IReadOnlyList<AdminCosmeticItemDto>> GetItemsAsync(CancellationToken cancellationToken)
    {
        return await db.CosmeticItems
            .AsNoTracking()
            .OrderBy(x => x.Category)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new AdminCosmeticItemDto(
                x.Id,
                x.Key,
                x.Name,
                x.Category,
                x.Rarity,
                x.AssetPath,
                x.PreviewAssetPath,
                x.UnlockType,
                x.UnlockCondition,
                x.UnlockConditionJson,
                x.CompatibilityRulesJson,
                x.CoinPrice,
                x.SeasonId,
                x.IsDefault,
                x.IsActive,
                x.IsHidden,
                x.SortOrder,
                x.AssetVersion,
                x.ReleaseDate,
                x.RetirementDate))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminCosmeticItemDto> UpsertItemAsync(int? id, UpsertCosmeticItemRequest request, string? actorUserId, CancellationToken cancellationToken)
    {
        var entity = id.HasValue
            ? await db.CosmeticItems.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
            : null;
        var beforeJson = entity is null ? null : JsonSerializer.Serialize(entity, SerializerOptions);

        if (entity is null)
        {
            entity = new CosmeticItem { CreatedAt = DateTime.UtcNow };
            db.CosmeticItems.Add(entity);
        }

        entity.Key = request.Key.Trim();
        entity.Name = request.Name.Trim();
        entity.Category = request.Category.Trim().ToLowerInvariant();
        entity.Rarity = request.Rarity.Trim().ToLowerInvariant();
        entity.AssetPath = request.AssetPath.Trim();
        entity.PreviewAssetPath = request.PreviewAssetPath?.Trim();
        entity.UnlockType = request.UnlockType.Trim().ToLowerInvariant();
        entity.UnlockCondition = request.UnlockCondition?.Trim();
        entity.UnlockConditionJson = request.UnlockConditionJson;
        entity.CompatibilityRulesJson = request.CompatibilityRulesJson;
        entity.CoinPrice = request.CoinPrice;
        entity.SeasonId = request.SeasonId;
        entity.IsDefault = request.IsDefault;
        entity.IsActive = request.IsActive;
        entity.IsHidden = request.IsHidden;
        entity.SortOrder = request.SortOrder;
        entity.AssetVersion = string.IsNullOrWhiteSpace(request.AssetVersion) ? "1" : request.AssetVersion.Trim();
        entity.ReleaseDate = request.ReleaseDate;
        entity.RetirementDate = request.RetirementDate;
        entity.UpdatedAt = DateTime.UtcNow;

        await WriteAuditAsync("upsert_item", actorUserId, nameof(CosmeticItem), entity.Id.ToString(), beforeJson, JsonSerializer.Serialize(entity, SerializerOptions));
        await db.SaveChangesAsync(cancellationToken);

        return MapAdminItem(entity);
    }

    public async Task<AdminCosmeticItemDto> ReleaseItemAsync(int id, string? actorUserId, CancellationToken cancellationToken)
    {
        var entity = await db.CosmeticItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Cosmetic item not found.");
        var beforeJson = JsonSerializer.Serialize(entity, SerializerOptions);

        entity.IsActive = true;
        entity.IsHidden = false;
        entity.ReleaseDate ??= DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        await WriteAuditAsync("release_item", actorUserId, nameof(CosmeticItem), entity.Id.ToString(), beforeJson, JsonSerializer.Serialize(entity, SerializerOptions));
        await db.SaveChangesAsync(cancellationToken);
        return MapAdminItem(entity);
    }

    public async Task<AdminCosmeticItemDto> RetireItemAsync(int id, string? actorUserId, CancellationToken cancellationToken)
    {
        var entity = await db.CosmeticItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Cosmetic item not found.");
        var beforeJson = JsonSerializer.Serialize(entity, SerializerOptions);

        entity.IsActive = false;
        entity.RetirementDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        await WriteAuditAsync("retire_item", actorUserId, nameof(CosmeticItem), entity.Id.ToString(), beforeJson, JsonSerializer.Serialize(entity, SerializerOptions));
        await db.SaveChangesAsync(cancellationToken);
        return MapAdminItem(entity);
    }

    public async Task<IReadOnlyList<CosmeticRewardRuleDto>> GetRewardRulesAsync(CancellationToken cancellationToken)
    {
        return await db.CosmeticRewardRules
            .AsNoTracking()
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Key)
            .Select(x => new CosmeticRewardRuleDto(
                x.Id,
                x.Key,
                x.SourceType,
                x.ConditionJson,
                x.RewardType,
                x.RewardPayloadJson,
                x.Priority,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<CosmeticRewardRuleDto> UpsertRewardRuleAsync(int? id, UpsertCosmeticRewardRuleRequest request, string? actorUserId, CancellationToken cancellationToken)
    {
        var entity = id.HasValue
            ? await db.CosmeticRewardRules.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
            : null;
        var beforeJson = entity is null ? null : JsonSerializer.Serialize(entity, SerializerOptions);

        if (entity is null)
        {
            entity = new CosmeticRewardRule();
            db.CosmeticRewardRules.Add(entity);
        }

        entity.Key = request.Key.Trim();
        entity.SourceType = request.SourceType.Trim().ToLowerInvariant();
        entity.ConditionJson = request.ConditionJson;
        entity.RewardType = request.RewardType.Trim().ToLowerInvariant();
        entity.RewardPayloadJson = request.RewardPayloadJson;
        entity.Priority = request.Priority;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await WriteAuditAsync("upsert_reward_rule", actorUserId, nameof(CosmeticRewardRule), entity.Id.ToString(), beforeJson, JsonSerializer.Serialize(entity, SerializerOptions));
        await db.SaveChangesAsync(cancellationToken);

        return new CosmeticRewardRuleDto(entity.Id, entity.Key, entity.SourceType, entity.ConditionJson, entity.RewardType, entity.RewardPayloadJson, entity.Priority, entity.IsActive);
    }

    public async Task<IReadOnlyList<CosmeticSeasonDto>> GetAdminSeasonsAsync(CancellationToken cancellationToken)
        => await GetSeasonsAsync(activeOnly: false, cancellationToken);

    public async Task<CosmeticSeasonDto> UpsertSeasonAsync(int? id, UpsertCosmeticSeasonRequest request, string? actorUserId, CancellationToken cancellationToken)
    {
        var entity = id.HasValue
            ? await db.CosmeticSeasons.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
            : null;
        var beforeJson = entity is null ? null : JsonSerializer.Serialize(entity, SerializerOptions);

        if (entity is null)
        {
            entity = new CosmeticSeason();
            db.CosmeticSeasons.Add(entity);
        }

        entity.Key = request.Key.Trim();
        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.Theme = request.Theme?.Trim();
        entity.ThemeAssetPath = request.ThemeAssetPath?.Trim();
        entity.Status = request.Status.Trim().ToLowerInvariant();
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.RewardLockAt = request.RewardLockAt;
        entity.ArchiveAt = request.ArchiveAt;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await WriteAuditAsync("upsert_season", actorUserId, nameof(CosmeticSeason), entity.Id.ToString(), beforeJson, JsonSerializer.Serialize(entity, SerializerOptions));
        await db.SaveChangesAsync(cancellationToken);

        return MapSeason(entity);
    }

    public async Task<CosmeticSeasonDto> ActivateSeasonAsync(int id, string? actorUserId, CancellationToken cancellationToken)
    {
        var entity = await db.CosmeticSeasons.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Cosmetic season not found.");
        var beforeJson = JsonSerializer.Serialize(entity, SerializerOptions);

        entity.Status = CosmeticSeasonStatuses.Active;
        entity.IsActive = true;
        entity.UpdatedAt = DateTime.UtcNow;

        await WriteAuditAsync("activate_season", actorUserId, nameof(CosmeticSeason), entity.Id.ToString(), beforeJson, JsonSerializer.Serialize(entity, SerializerOptions));
        await db.SaveChangesAsync(cancellationToken);
        return MapSeason(entity);
    }

    public async Task<CosmeticSeasonDto> ArchiveSeasonAsync(int id, string? actorUserId, CancellationToken cancellationToken)
    {
        var entity = await db.CosmeticSeasons.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Cosmetic season not found.");
        var beforeJson = JsonSerializer.Serialize(entity, SerializerOptions);

        entity.Status = CosmeticSeasonStatuses.Archived;
        entity.IsActive = false;
        entity.ArchiveAt ??= DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        await WriteAuditAsync("archive_season", actorUserId, nameof(CosmeticSeason), entity.Id.ToString(), beforeJson, JsonSerializer.Serialize(entity, SerializerOptions));
        await db.SaveChangesAsync(cancellationToken);
        return MapSeason(entity);
    }

    public async Task<RewardTrackTierDto> UpsertRewardTrackAsync(int? id, UpsertRewardTrackEntryRequest request, string? actorUserId, CancellationToken cancellationToken)
    {
        var entity = id.HasValue
            ? await db.SeasonRewardTrackEntries.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
            : null;
        var beforeJson = entity is null ? null : JsonSerializer.Serialize(entity, SerializerOptions);

        if (entity is null)
        {
            entity = new SeasonRewardTrackEntry();
            db.SeasonRewardTrackEntries.Add(entity);
        }

        entity.SeasonId = request.SeasonId;
        entity.TrackType = request.TrackType.Trim().ToLowerInvariant();
        entity.Tier = request.Tier;
        entity.XpRequired = request.XpRequired;
        entity.RewardType = request.RewardType.Trim().ToLowerInvariant();
        entity.RewardPayloadJson = request.RewardPayloadJson;
        entity.IsActive = request.IsActive;

        await WriteAuditAsync("upsert_reward_track", actorUserId, nameof(SeasonRewardTrackEntry), entity.Id.ToString(), beforeJson, JsonSerializer.Serialize(entity, SerializerOptions));
        await db.SaveChangesAsync(cancellationToken);

        return new RewardTrackTierDto(entity.Id, entity.Tier, entity.XpRequired, entity.TrackType, entity.RewardType, entity.RewardPayloadJson, false, false, false);
    }

    public async Task<CosmeticAnalyticsSummaryDto> GetAnalyticsSummaryAsync(CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddDays(-7);

        var totalOwnedItems = await db.UserCosmeticInventories.AsNoTracking().LongCountAsync(x => !x.IsRevoked, cancellationToken);
        var activeCustomizedUsers = await db.UserAppearanceProjections.AsNoTracking().LongCountAsync(
            x => x.SkinId != null || x.HairId != null || x.ClothingId != null || x.AccessoryId != null || x.FrameId != null || x.BackgroundId != null || x.EffectId != null || x.LeaderboardDecorationId != null,
            cancellationToken);
        var unlockEvents7d = await db.CosmeticTelemetryEvents.AsNoTracking().LongCountAsync(x => x.EventType == CosmeticTelemetryEventTypes.Unlock && x.OccurredAtUtc >= since, cancellationToken);
        var equipEvents7d = await db.CosmeticTelemetryEvents.AsNoTracking().LongCountAsync(x => x.EventType == CosmeticTelemetryEventTypes.Equip && x.OccurredAtUtc >= since, cancellationToken);
        var purchaseEvents7d = await db.CosmeticTelemetryEvents.AsNoTracking().LongCountAsync(x => x.EventType == CosmeticTelemetryEventTypes.Purchase && x.OccurredAtUtc >= since, cancellationToken);

        var unlocksBySource = await db.UserCosmeticInventories
            .AsNoTracking()
            .Where(x => x.UnlockedAt >= since && !x.IsRevoked)
            .GroupBy(x => x.Source)
            .Select(x => new { x.Key, Count = x.LongCount() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var equipByCategory = await db.UserAppearanceProjections
            .AsNoTracking()
            .SelectMany(x => new[]
            {
                x.SkinId != null ? CosmeticCategories.Skin : null,
                x.HairId != null ? CosmeticCategories.Hair : null,
                x.ClothingId != null ? CosmeticCategories.Clothing : null,
                x.AccessoryId != null ? CosmeticCategories.Accessory : null,
                x.EmojiId != null ? CosmeticCategories.Emoji : null,
                x.FrameId != null ? CosmeticCategories.Frame : null,
                x.BackgroundId != null ? CosmeticCategories.Background : null,
                x.EffectId != null ? CosmeticCategories.Effect : null,
                x.LeaderboardDecorationId != null ? CosmeticCategories.LeaderboardDecoration : null
            })
            .Where(x => x != null)
            .GroupBy(x => x!)
            .Select(x => new { x.Key, Count = x.LongCount() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return new CosmeticAnalyticsSummaryDto(totalOwnedItems, activeCustomizedUsers, unlockEvents7d, equipEvents7d, purchaseEvents7d, unlocksBySource, equipByCategory);
    }

    private static AdminCosmeticItemDto MapAdminItem(CosmeticItem entity)
        => new(
            entity.Id, entity.Key, entity.Name, entity.Category, entity.Rarity, entity.AssetPath, entity.PreviewAssetPath,
            entity.UnlockType, entity.UnlockCondition, entity.UnlockConditionJson, entity.CompatibilityRulesJson, entity.CoinPrice,
            entity.SeasonId, entity.IsDefault, entity.IsActive, entity.IsHidden, entity.SortOrder, entity.AssetVersion,
            entity.ReleaseDate, entity.RetirementDate);

    private static CosmeticSeasonDto MapSeason(CosmeticSeason entity)
        => new(
            entity.Id,
            entity.Key,
            entity.Name,
            entity.Description,
            entity.Theme,
            entity.ThemeAssetPath,
            entity.Status,
            entity.StartDate,
            entity.EndDate,
            entity.IsActive,
            entity.Items.Count);
}
