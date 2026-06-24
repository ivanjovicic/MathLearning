namespace MathLearning.Application.DTOs.Cosmetics;

public record MobileCosmeticCatalogItemDto(
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
    bool IsActive,
    bool IsHidden,
    string AssetVersion
);

public record MobileCosmeticCatalogResponseDto(
    string CatalogVersion,
    IReadOnlyList<MobileCosmeticCatalogItemDto> Items
);

public record MobileCosmeticInventoryResponseDto(
    IReadOnlyList<string> ItemKeys,
    IReadOnlyDictionary<string, int> FragmentProgress
);

public record MobileCosmeticAvatarResponseDto(
    IReadOnlyDictionary<string, string?> Slots,
    long Version
);

public record MobileCosmeticAvatarUpdateRequest(
    IReadOnlyDictionary<string, string?> Slots
);
