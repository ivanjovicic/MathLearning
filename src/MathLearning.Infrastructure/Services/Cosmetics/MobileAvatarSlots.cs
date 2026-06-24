using MathLearning.Domain.Entities;

namespace MathLearning.Infrastructure.Services.Cosmetics;

internal static class MobileAvatarSlots
{
    public static readonly IReadOnlyList<string> Keys =
    [
        "skin",
        "hair",
        "clothing",
        "accessory",
        "emoji",
        "frame",
        "background",
        "effect",
        "leaderboardDecoration"
    ];

    private static readonly IReadOnlyDictionary<string, Func<UserAvatarConfig, int?>> Selectors =
        new Dictionary<string, Func<UserAvatarConfig, int?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["skin"] = config => config.SkinId,
            ["hair"] = config => config.HairId,
            ["clothing"] = config => config.ClothingId,
            ["accessory"] = config => config.AccessoryId,
            ["emoji"] = config => config.EmojiId,
            ["frame"] = config => config.FrameId,
            ["background"] = config => config.BackgroundId,
            ["effect"] = config => config.EffectId,
            ["leaderboardDecoration"] = config => config.LeaderboardDecorationId
        };

    public static IReadOnlyDictionary<string, string?> BuildSlotMap(
        UserAvatarConfig config,
        IReadOnlyDictionary<int, string> keyByItemId)
    {
        var slots = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var slotKey in Keys)
        {
            var itemId = Selectors[slotKey](config);
            if (!itemId.HasValue || !keyByItemId.TryGetValue(itemId.Value, out var itemKey))
            {
                slots[slotKey] = null;
                continue;
            }

            slots[slotKey] = itemKey;
        }

        return slots;
    }
}
