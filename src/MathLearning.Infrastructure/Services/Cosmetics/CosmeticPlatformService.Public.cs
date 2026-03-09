using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public sealed partial class CosmeticPlatformService
{
    public async Task<CosmeticCatalogResponseDto> GetCatalogAsync(
        string userId,
        string? category,
        string? rarity,
        int? seasonId,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultOwnershipAsync(userId, cancellationToken);

        var now = DateTime.UtcNow;
        var items = await GetCachedActiveCatalogItemsAsync(cancellationToken);
        var filtered = items
            .Where(x =>
                (string.IsNullOrWhiteSpace(category) || x.Category == category.Trim()) &&
                (string.IsNullOrWhiteSpace(rarity) || x.Rarity == rarity.Trim()) &&
                (!seasonId.HasValue || x.SeasonId == seasonId.Value) &&
                (x.ReleaseDate == null || x.ReleaseDate <= now) &&
                (x.RetirementDate == null || x.RetirementDate > now))
            .ToList();

        var ownedIds = (await db.UserCosmeticInventories
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsRevoked)
            .Select(x => x.CosmeticItemId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var config = await db.UserAvatarConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        var equippedIds = BuildEquippedSet(config);

        var responseItems = filtered
            .Select(x => new CosmeticCatalogItemDto(
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
                x.CoinPrice,
                x.SeasonId,
                x.IsDefault,
                false,
                false,
                x.IsActive,
                x.IsHidden,
                x.AssetVersion))
            .Select(x => x with
            {
                IsOwned = x.IsDefault || ownedIds.Contains(x.Id),
                IsEquipped = equippedIds.Contains(x.Id)
            })
            .ToList();

        var catalogVersion = await BuildCatalogVersionAsync(cancellationToken);
        return new CosmeticCatalogResponseDto(catalogVersion, responseItems);
    }

    public async Task<IReadOnlyList<CosmeticSeasonDto>> GetSeasonsAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = db.CosmeticSeasons.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderByDescending(x => x.StartDate)
            .Select(x => new CosmeticSeasonDto(
                x.Id,
                x.Key,
                x.Name,
                x.Description,
                x.Theme,
                x.ThemeAssetPath,
                x.Status,
                x.StartDate,
                x.EndDate,
                x.IsActive,
                x.Items.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<RewardTrackResponseDto?> GetRewardTrackAsync(
        string userId,
        int? seasonId,
        string trackType,
        CancellationToken cancellationToken)
    {
        var effectiveTrackType = string.IsNullOrWhiteSpace(trackType) ? CosmeticTrackTypes.Free : trackType.Trim().ToLowerInvariant();
        var season = await ResolveSeasonAsync(seasonId, cancellationToken);
        if (season is null)
        {
            return null;
        }

        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        var currentXp = profile?.Xp ?? 0;
        var claimPrefix = BuildRewardTrackSourceRef(season.Id, effectiveTrackType, string.Empty);
        var claimedRefs = await db.CosmeticRewardClaims
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.SourceType == CosmeticUnlockTypes.RewardTrack &&
                x.SourceRef.StartsWith(claimPrefix))
            .Select(x => x.SourceRef)
            .Distinct()
            .ToListAsync(cancellationToken);
        var claimedSet = claimedRefs.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tiers = await db.SeasonRewardTrackEntries
            .AsNoTracking()
            .Where(x => x.SeasonId == season.Id && x.TrackType == effectiveTrackType && x.IsActive)
            .OrderBy(x => x.Tier)
            .Select(x => new RewardTrackTierDto(
                x.Id,
                x.Tier,
                x.XpRequired,
                x.TrackType,
                x.RewardType,
                x.RewardPayloadJson,
                currentXp >= x.XpRequired,
                claimedSet.Contains(BuildRewardTrackSourceRef(season.Id, effectiveTrackType, x.Tier.ToString())),
                false))
            .ToListAsync(cancellationToken);

        tiers = tiers
            .Select(x => x with
            {
                CanClaim = x.IsUnlocked && !x.IsClaimed
            })
            .ToList();

        var currentTier = tiers.Where(x => x.IsUnlocked).Select(x => x.Tier).DefaultIfEmpty(0).Max();
        var claimableTierCount = tiers.Count(x => x.CanClaim);
        return new RewardTrackResponseDto(season.Id, effectiveTrackType, currentXp, currentTier, claimableTierCount, tiers);
    }

    public async Task<CosmeticInventoryResponseDto> GetInventoryAsync(
        string userId,
        string? category,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultOwnershipAsync(userId, cancellationToken);

        var query = db.UserCosmeticInventories
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsRevoked)
            .Include(x => x.CosmeticItem)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.CosmeticItem.Category == category.Trim());
        }

        var config = await db.UserAvatarConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        var equipped = ToAvatarConfigDto(config);
        var equippedIds = BuildEquippedSet(config);

        var items = await query
            .OrderByDescending(x => x.UnlockedAt)
            .Select(x => new InventoryItemDto(
                x.CosmeticItemId,
                x.CosmeticItem.Key,
                x.CosmeticItem.Name,
                x.CosmeticItem.Category,
                x.CosmeticItem.Rarity,
                x.CosmeticItem.AssetPath,
                x.CosmeticItem.PreviewAssetPath,
                x.Source,
                x.SourceRef,
                x.GrantReason,
                x.UnlockedAt,
                false,
                x.IsRevoked))
            .ToListAsync(cancellationToken);

        items = items.Select(x => x with { IsEquipped = equippedIds.Contains(x.CosmeticItemId) }).ToList();
        return new CosmeticInventoryResponseDto(userId, items.Count, items, equipped);
    }

    public async Task<AvatarAppearanceDto> GetAvatarAsync(string userId, CancellationToken cancellationToken)
    {
        await EnsureDefaultOwnershipAsync(userId, cancellationToken);
        await EnsureAvatarConfigAsync(userId, cancellationToken);
        return await GetPublicAppearanceAsync(userId, cancellationToken);
    }

    public async Task<AvatarAppearanceDto> GetPublicAppearanceAsync(string userId, CancellationToken cancellationToken)
    {
        var projection = await cache.GetOrCreateAsync(
            GetAppearanceCacheKey(userId),
            async ct => await db.UserAppearanceProjections.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct),
            AppearanceCacheTtl,
            TimeSpan.FromMinutes(2),
            cancellationToken);
        if (projection is null)
        {
            projection = await RebuildAppearanceProjectionAsync(userId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return new AvatarAppearanceDto(
            new AvatarConfigDto(
                projection.SkinId,
                projection.HairId,
                projection.ClothingId,
                projection.AccessoryId,
                projection.EmojiId,
                projection.FrameId,
                projection.BackgroundId,
                projection.EffectId,
                projection.LeaderboardDecorationId,
                projection.AvatarVersion),
            projection.SkinAssetPath,
            projection.HairAssetPath,
            projection.ClothingAssetPath,
            projection.AccessoryAssetPath,
            projection.EmojiAssetPath,
            projection.FrameAssetPath,
            projection.BackgroundAssetPath,
            projection.EffectAssetPath,
            projection.LeaderboardDecorationAssetPath);
    }

    public async Task<AvatarAppearanceDto> UpdateAvatarAsync(
        string userId,
        UpdateAvatarConfigRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultOwnershipAsync(userId, cancellationToken);

        var config = await EnsureAvatarConfigAsync(userId, cancellationToken);
        var changes = new List<EquipCosmeticBatchChangeDto>
        {
            new(CosmeticCategories.Skin, request.SkinId),
            new(CosmeticCategories.Hair, request.HairId),
            new(CosmeticCategories.Clothing, request.ClothingId),
            new(CosmeticCategories.Accessory, request.AccessoryId),
            new(CosmeticCategories.Emoji, request.EmojiId),
            new(CosmeticCategories.Frame, request.FrameId),
            new(CosmeticCategories.Background, request.BackgroundId),
            new(CosmeticCategories.Effect, request.EffectId),
            new(CosmeticCategories.LeaderboardDecoration, request.LeaderboardDecorationId)
        };

        await ApplyAvatarChangesAsync(userId, config, changes, cancellationToken);
        return await GetPublicAppearanceAsync(userId, cancellationToken);
    }

    public async Task<AvatarAppearanceDto> EquipSlotAsync(
        string userId,
        EquipCosmeticRequest request,
        CancellationToken cancellationToken)
    {
        var batch = new EquipCosmeticBatchRequest([new EquipCosmeticBatchChangeDto(request.Slot, request.CosmeticItemId)]);
        return await EquipBatchAsync(userId, batch, cancellationToken);
    }

    public async Task<AvatarAppearanceDto> EquipBatchAsync(
        string userId,
        EquipCosmeticBatchRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultOwnershipAsync(userId, cancellationToken);
        var config = await EnsureAvatarConfigAsync(userId, cancellationToken);
        await ApplyAvatarChangesAsync(userId, config, request.Changes, cancellationToken);
        return await GetPublicAppearanceAsync(userId, cancellationToken);
    }

    public async Task<PurchaseCosmeticResponse> PurchaseAsync(
        string userId,
        PurchaseCosmeticRequest request,
        CancellationToken cancellationToken)
    {
        var item = await db.CosmeticItems.FirstOrDefaultAsync(x => x.Id == request.CosmeticItemId, cancellationToken)
            ?? throw new InvalidOperationException("Cosmetic item not found.");

        if (!item.CoinPrice.HasValue || item.CoinPrice.Value <= 0)
        {
            throw new InvalidOperationException("This item cannot be purchased with coins.");
        }

        if (item.RetirementDate.HasValue && item.RetirementDate.Value <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("This item is retired.");
        }

        if (await db.UserCosmeticInventories.AnyAsync(x => x.UserId == userId && x.CosmeticItemId == item.Id && !x.IsRevoked, cancellationToken))
        {
            throw new InvalidOperationException("Item already owned.");
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Profile not found.");
        if (profile.Coins < item.CoinPrice.Value)
        {
            throw new InvalidOperationException("Insufficient coins.");
        }

        profile.Coins -= item.CoinPrice.Value;
        profile.TotalCoinsSpent += item.CoinPrice.Value;
        profile.UpdatedAt = DateTime.UtcNow;

        await TryGrantItemAsync(userId, item, CosmeticUnlockTypes.Shop, $"shop:{item.Id}", $"Purchased {item.Name}", cancellationToken);
        await LogTelemetryAsync(CosmeticTelemetryEventTypes.Purchase, userId, item.Id, item.SeasonId, new { coinsSpent = item.CoinPrice.Value });
        await db.SaveChangesAsync(cancellationToken);

        return new PurchaseCosmeticResponse(true, $"Purchased '{item.Name}' for {item.CoinPrice.Value} coins", profile.Coins);
    }
}
