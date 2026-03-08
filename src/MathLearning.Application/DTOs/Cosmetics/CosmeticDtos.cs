namespace MathLearning.Application.DTOs.Cosmetics;

// ─── Cosmetic Item ───

public record CosmeticItemDto(
    int Id,
    string Name,
    string Category,
    string Rarity,
    string AssetPath,
    string? PreviewAssetPath,
    string UnlockType,
    string? UnlockCondition,
    int? CoinPrice,
    int? SeasonId,
    bool IsDefault,
    bool IsOwned
);

// ─── Inventory ───

public record InventoryItemDto(
    int CosmeticItemId,
    string Name,
    string Category,
    string Rarity,
    string AssetPath,
    string? PreviewAssetPath,
    string Source,
    DateTime UnlockedAt
);

// ─── Avatar Config ───

public record AvatarConfigDto(
    int? SkinId,
    int? HairId,
    int? ClothingId,
    int? AccessoryId,
    int? EmojiId,
    int? FrameId,
    int? BackgroundId,
    int? EffectId
);

public record UpdateAvatarConfigRequest(
    int? SkinId,
    int? HairId,
    int? ClothingId,
    int? AccessoryId,
    int? EmojiId,
    int? FrameId,
    int? BackgroundId,
    int? EffectId
);

// ─── Equip single slot ───

public record EquipCosmeticRequest(
    string Slot,       // "skin", "hair", "clothing", "accessory", "emoji", "frame", "background", "effect"
    int? CosmeticItemId // null = unequip
);

// ─── Purchase ───

public record PurchaseCosmeticRequest(
    int CosmeticItemId
);

public record PurchaseCosmeticResponse(
    bool Success,
    string Message,
    int RemainingCoins
);

// ─── Season ───

public record CosmeticSeasonDto(
    int Id,
    string Name,
    string? Description,
    string? ThemeAssetPath,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    int ItemCount
);

// ─── Avatar summary for leaderboard/profile ───

public record AvatarSummaryDto(
    string? SkinAsset,
    string? HairAsset,
    string? ClothingAsset,
    string? AccessoryAsset,
    string? EmojiAsset,
    string? FrameAsset,
    string? BackgroundAsset,
    string? EffectAsset
);
