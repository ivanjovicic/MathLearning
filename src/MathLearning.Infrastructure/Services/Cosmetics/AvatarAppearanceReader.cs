using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public sealed class AvatarAppearanceReader : IAvatarAppearanceReader
{
    private readonly ApiDbContext db;

    public AvatarAppearanceReader(ApiDbContext db)
    {
        this.db = db;
    }

    public async Task<AvatarAppearanceDto?> GetAppearanceAsync(string userId, CancellationToken cancellationToken = default)
    {
        var map = await GetAppearancesAsync([userId], cancellationToken);
        return map.TryGetValue(userId, out var appearance) ? appearance : null;
    }

    public async Task<IReadOnlyDictionary<string, AvatarAppearanceDto>> GetAppearancesAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, AvatarAppearanceDto>(StringComparer.Ordinal);
        }

        var configs = await db.UserAvatarConfigs
            .AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .ToListAsync(cancellationToken);

        if (configs.Count == 0)
        {
            return new Dictionary<string, AvatarAppearanceDto>(StringComparer.Ordinal);
        }

        var equippedItemIds = configs
            .SelectMany(BuildEquippedItemIds)
            .Distinct()
            .ToList();

        var items = equippedItemIds.Count == 0
            ? new Dictionary<int, CosmeticItem>()
            : await db.CosmeticItems
                .AsNoTracking()
                .Where(x => equippedItemIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        return configs.ToDictionary(
            x => x.UserId,
            x => MapAppearance(x, items),
            StringComparer.Ordinal);
    }

    internal static AvatarAppearanceDto MapAppearance(
        UserAvatarConfig config,
        IReadOnlyDictionary<int, CosmeticItem> items)
    {
        return new AvatarAppearanceDto(
            new AvatarConfigDto(
                config.SkinId,
                config.HairId,
                config.ClothingId,
                config.AccessoryId,
                config.EmojiId,
                config.FrameId,
                config.BackgroundId,
                config.EffectId,
                config.LeaderboardDecorationId,
                config.Version),
            ResolveAsset(items, config.SkinId),
            ResolveAsset(items, config.HairId),
            ResolveAsset(items, config.ClothingId),
            ResolveAsset(items, config.AccessoryId),
            ResolveAsset(items, config.EmojiId),
            ResolveAsset(items, config.FrameId),
            ResolveAsset(items, config.BackgroundId),
            ResolveAsset(items, config.EffectId),
            ResolveAsset(items, config.LeaderboardDecorationId));
    }

    internal static IEnumerable<int> BuildEquippedItemIds(UserAvatarConfig config)
    {
        if (config.SkinId.HasValue) yield return config.SkinId.Value;
        if (config.HairId.HasValue) yield return config.HairId.Value;
        if (config.ClothingId.HasValue) yield return config.ClothingId.Value;
        if (config.AccessoryId.HasValue) yield return config.AccessoryId.Value;
        if (config.EmojiId.HasValue) yield return config.EmojiId.Value;
        if (config.FrameId.HasValue) yield return config.FrameId.Value;
        if (config.BackgroundId.HasValue) yield return config.BackgroundId.Value;
        if (config.EffectId.HasValue) yield return config.EffectId.Value;
        if (config.LeaderboardDecorationId.HasValue) yield return config.LeaderboardDecorationId.Value;
    }

    private static string? ResolveAsset(IReadOnlyDictionary<int, CosmeticItem> items, int? itemId)
    {
        if (!itemId.HasValue)
        {
            return null;
        }

        return items.TryGetValue(itemId.Value, out var item) ? item.AssetPath : null;
    }
}
