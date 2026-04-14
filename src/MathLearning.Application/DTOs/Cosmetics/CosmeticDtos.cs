namespace MathLearning.Application.DTOs.Cosmetics;

public record CosmeticCatalogItemDto(
    int Id,
    string Key,
    string Name,
    string Category,
    string Rarity,
    string AssetPath,
    string? PreviewAssetPath,
    string UnlockType,
    string? UnlockCondition,
    string? UnlockConditionJson,
    int? CoinPrice,
    int? SeasonId,
    bool IsDefault,
    bool IsOwned,
    bool IsEquipped,
    bool IsActive,
    bool IsHidden,
    string AssetVersion
);

public record CosmeticCatalogResponseDto(
    string CatalogVersion,
    IReadOnlyList<CosmeticCatalogItemDto> Items
);

public record InventoryItemDto(
    int CosmeticItemId,
    string Key,
    string Name,
    string Category,
    string Rarity,
    string AssetPath,
    string? PreviewAssetPath,
    string Source,
    string? SourceRef,
    string? GrantReason,
    DateTime UnlockedAt,
    bool IsEquipped,
    bool IsRevoked
);

public record CosmeticInventoryResponseDto(
    string UserId,
    int OwnedCount,
    IReadOnlyList<InventoryItemDto> Items,
    AvatarConfigDto Equipped
);

public record AvatarConfigDto(
    int? SkinId,
    int? HairId,
    int? ClothingId,
    int? AccessoryId,
    int? EmojiId,
    int? FrameId,
    int? BackgroundId,
    int? EffectId,
    int? LeaderboardDecorationId,
    long Version
);

public record AvatarAppearanceDto(
    AvatarConfigDto Equipped,
    string? SkinAsset,
    string? HairAsset,
    string? ClothingAsset,
    string? AccessoryAsset,
    string? EmojiAsset,
    string? FrameAsset,
    string? BackgroundAsset,
    string? EffectAsset,
    string? LeaderboardDecorationAsset
);

public record UpdateAvatarConfigRequest(
    int? SkinId,
    int? HairId,
    int? ClothingId,
    int? AccessoryId,
    int? EmojiId,
    int? FrameId,
    int? BackgroundId,
    int? EffectId,
    int? LeaderboardDecorationId
);

public record EquipCosmeticRequest(
    string Slot,
    int? CosmeticItemId
);

public record EquipCosmeticBatchChangeDto(
    string Slot,
    int? CosmeticItemId
);

public record EquipCosmeticBatchRequest(
    IReadOnlyList<EquipCosmeticBatchChangeDto> Changes
);

public record PurchaseCosmeticRequest(
    int CosmeticItemId
);

public record PurchaseCosmeticResponse(
    bool Success,
    string Message,
    int RemainingCoins
);

public record CosmeticSeasonDto(
    int Id,
    string Key,
    string Name,
    string? Description,
    string? Theme,
    string? ThemeAssetPath,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    int ItemCount
);

public record RewardTrackTierDto(
    int Id,
    int Tier,
    int XpRequired,
    string TrackType,
    string RewardType,
    string RewardPayloadJson,
    bool IsUnlocked,
    bool IsClaimed,
    bool CanClaim
);

public record RewardTrackResponseDto(
    int SeasonId,
    string TrackType,
    int CurrentXp,
    int CurrentTier,
    int ClaimableTierCount,
    IReadOnlyList<RewardTrackTierDto> Tiers
);

public record CosmeticUnlockResultDto(
    int CosmeticItemId,
    string Key,
    string Name,
    string Category,
    string Rarity,
    string SourceType,
    string SourceRef,
    DateTime UnlockedAtUtc
);

public record CosmeticRewardSourceRequest(
    string UserId,
    string SourceType,
    string SourceRef,
    string? PayloadJson
);

public record ClaimRewardTrackTierRequest(
    int? SeasonId,
    string TrackType,
    int Tier
);

public record ClaimRewardTrackTierResponse(
    bool Success,
    bool AlreadyClaimed,
    int SeasonId,
    string TrackType,
    int Tier,
    IReadOnlyList<CosmeticUnlockResultDto> Rewards
);

public record AdminCosmeticItemDto(
    int Id,
    string Key,
    string Name,
    string Category,
    string Rarity,
    string AssetPath,
    string? PreviewAssetPath,
    string UnlockType,
    string? UnlockCondition,
    string? UnlockConditionJson,
    string? CompatibilityRulesJson,
    int? CoinPrice,
    int? SeasonId,
    bool IsDefault,
    bool IsActive,
    bool IsHidden,
    int SortOrder,
    string AssetVersion,
    DateTime? ReleaseDate,
    DateTime? RetirementDate
);

public record UpsertCosmeticItemRequest(
    string Key,
    string Name,
    string Category,
    string Rarity,
    string AssetPath,
    string? PreviewAssetPath,
    string UnlockType,
    string? UnlockCondition,
    string? UnlockConditionJson,
    string? CompatibilityRulesJson,
    int? CoinPrice,
    int? SeasonId,
    bool IsDefault,
    bool IsActive,
    bool IsHidden,
    int SortOrder,
    string AssetVersion,
    DateTime? ReleaseDate,
    DateTime? RetirementDate
);

public record CosmeticRewardRuleDto(
    int Id,
    string Key,
    string SourceType,
    string? ConditionJson,
    string RewardType,
    string RewardPayloadJson,
    int Priority,
    bool IsActive
);

public record UpsertCosmeticRewardRuleRequest(
    string Key,
    string SourceType,
    string? ConditionJson,
    string RewardType,
    string RewardPayloadJson,
    int Priority,
    bool IsActive
);

public record UpsertCosmeticSeasonRequest(
    string Key,
    string Name,
    string? Description,
    string? Theme,
    string? ThemeAssetPath,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    DateTime? RewardLockAt,
    DateTime? ArchiveAt,
    bool IsActive
);

public record UpsertRewardTrackEntryRequest(
    int SeasonId,
    string TrackType,
    int Tier,
    int XpRequired,
    string RewardType,
    string RewardPayloadJson,
    bool IsActive
);

public record CosmeticAnalyticsSummaryDto(
    long TotalOwnedItems,
    long ActiveCustomizedUsers,
    long UnlockEvents7d,
    long EquipEvents7d,
    long PurchaseEvents7d,
    IReadOnlyDictionary<string, long> UnlocksBySource,
    IReadOnlyDictionary<string, long> EquipByCategory
);
