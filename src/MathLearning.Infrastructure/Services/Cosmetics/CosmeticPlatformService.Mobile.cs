using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public sealed partial class CosmeticPlatformService
{
    private static readonly IReadOnlyDictionary<string, string> MobileAvatarSlotAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["skin"] = CosmeticCategories.Skin,
            ["hair"] = CosmeticCategories.Hair,
            ["clothing"] = CosmeticCategories.Clothing,
            ["accessory"] = CosmeticCategories.Accessory,
            ["emoji"] = CosmeticCategories.Emoji,
            ["frame"] = CosmeticCategories.Frame,
            ["background"] = CosmeticCategories.Background,
            ["effect"] = CosmeticCategories.Effect,
            ["leaderboarddecoration"] = CosmeticCategories.LeaderboardDecoration,
            ["leaderboard_decoration"] = CosmeticCategories.LeaderboardDecoration
        };

    public async Task<MobileCosmeticCatalogResponseDto> GetPublishedCatalogAsync(
        string? category,
        string? rarity,
        int? seasonId,
        CancellationToken cancellationToken)
    {
        var catalogVersion = await BuildCatalogVersionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var items = await GetCachedActiveCatalogItemsAsync(catalogVersion, cancellationToken);
        var filtered = items
            .Where(x =>
                (string.IsNullOrWhiteSpace(category) || x.Category == category.Trim()) &&
                (string.IsNullOrWhiteSpace(rarity) || x.Rarity == rarity.Trim()) &&
                (!seasonId.HasValue || x.SeasonId == seasonId.Value) &&
                (x.ReleaseDate == null || x.ReleaseDate <= now) &&
                (x.RetirementDate == null || x.RetirementDate > now))
            .Select(x => new MobileCosmeticCatalogItemDto(
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
                x.IsActive,
                x.IsHidden,
                x.AssetVersion))
            .ToList();

        return new MobileCosmeticCatalogResponseDto(catalogVersion, filtered);
    }

    public async Task<MobileCosmeticInventoryResponseDto> GetMobileInventoryAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultOwnershipAsync(userId, cancellationToken);

        var itemKeys = await db.UserCosmeticInventories
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsRevoked)
            .Join(
                db.CosmeticItems.AsNoTracking(),
                inventory => inventory.CosmeticItemId,
                item => item.Id,
                (_, item) => item.Key)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var fragmentProgress = await CosmeticsFragmentService.LoadFragmentProgressByLabelAsync(db, userId, cancellationToken);

        return new MobileCosmeticInventoryResponseDto(itemKeys, fragmentProgress);
    }

    public async Task<MobileCosmeticAvatarResponseDto> GetMobileAvatarAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultOwnershipAsync(userId, cancellationToken);
        var config = await EnsureAvatarConfigAsync(userId, cancellationToken);
        return await BuildMobileAvatarResponseAsync(config, cancellationToken);
    }

    public async Task<MobileCosmeticAvatarResponseDto> UpdateMobileAvatarAsync(
        string userId,
        MobileCosmeticAvatarUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultOwnershipAsync(userId, cancellationToken);
        var config = await EnsureAvatarConfigAsync(userId, cancellationToken);
        var changes = await BuildMobileAvatarChangesAsync(request.Slots, cancellationToken);
        await ApplyAvatarChangesAsync(userId, config, changes, cancellationToken);
        return await BuildMobileAvatarResponseAsync(config, cancellationToken);
    }

    private async Task<MobileCosmeticAvatarResponseDto> BuildMobileAvatarResponseAsync(
        UserAvatarConfig config,
        CancellationToken cancellationToken)
    {
        var equippedIds = new Dictionary<string, int?>
        {
            [CosmeticCategories.Skin] = config.SkinId,
            [CosmeticCategories.Hair] = config.HairId,
            [CosmeticCategories.Clothing] = config.ClothingId,
            [CosmeticCategories.Accessory] = config.AccessoryId,
            [CosmeticCategories.Emoji] = config.EmojiId,
            [CosmeticCategories.Frame] = config.FrameId,
            [CosmeticCategories.Background] = config.BackgroundId,
            [CosmeticCategories.Effect] = config.EffectId,
            [CosmeticCategories.LeaderboardDecoration] = config.LeaderboardDecorationId
        };

        var ids = equippedIds.Values.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var keyById = ids.Count == 0
            ? new Dictionary<int, string>()
            : await db.CosmeticItems
                .AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Key, cancellationToken);

        return new MobileCosmeticAvatarResponseDto(
            MobileAvatarSlots.BuildSlotMap(config, keyById),
            config.Version);
    }

    private async Task<IReadOnlyList<EquipCosmeticBatchChangeDto>> BuildMobileAvatarChangesAsync(
        IReadOnlyDictionary<string, string?> slots,
        CancellationToken cancellationToken)
    {
        if (slots.Count == 0)
        {
            return Array.Empty<EquipCosmeticBatchChangeDto>();
        }

        var normalizedSlots = new List<(string Slot, string? ItemKey)>();
        foreach (var (rawSlot, itemKey) in slots)
        {
            if (!TryNormalizeMobileAvatarSlot(rawSlot, out var slot))
            {
                throw new InvalidOperationException($"Avatar slot '{rawSlot}' is not supported.");
            }

            normalizedSlots.Add((slot, string.IsNullOrWhiteSpace(itemKey) ? null : itemKey.Trim()));
        }

        var requestedKeys = normalizedSlots
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemKey))
            .Select(x => x.ItemKey!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var itemMap = requestedKeys.Count == 0
            ? new Dictionary<string, CosmeticItem>(StringComparer.OrdinalIgnoreCase)
            : await db.CosmeticItems
                .Where(x => requestedKeys.Contains(x.Key))
                .ToDictionaryAsync(x => x.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);

        return normalizedSlots
            .Select(change =>
            {
                if (change.ItemKey is null)
                {
                    return new EquipCosmeticBatchChangeDto(change.Slot, null);
                }

                if (!itemMap.TryGetValue(change.ItemKey, out var item))
                {
                    throw new InvalidOperationException($"Cosmetic item '{change.ItemKey}' was not found.");
                }

                return new EquipCosmeticBatchChangeDto(change.Slot, item.Id);
            })
            .ToList();
    }

    private static bool TryNormalizeMobileAvatarSlot(string rawSlot, out string slot)
    {
        slot = string.Empty;
        if (string.IsNullOrWhiteSpace(rawSlot))
        {
            return false;
        }

        if (MobileAvatarSlotAliases.TryGetValue(rawSlot.Trim(), out var normalizedSlot))
        {
            slot = normalizedSlot;
            return true;
        }

        return false;
    }
}
