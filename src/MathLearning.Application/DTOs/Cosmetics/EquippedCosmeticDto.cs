namespace MathLearning.Application.DTOs.Cosmetics;

/// <summary>
/// Rich metadata for an equipped cosmetic item.
/// Prevents frontend from guessing names, rarities, or sources from IDs.
/// </summary>
public record EquippedCosmeticDto(
    int ItemId,
    string Key,
    string Name,
    string Category,
    string Rarity,
    string? UnlockSource,
    string? AssetPath,
    string? PreviewAssetPath = null,
    bool IsDefault = false,
    string? CompatibilityInfo = null
);

/// <summary>
/// Complete cosmetic loadout with full metadata for each slot.
/// Organized by cosmetic category for clarity and future extensibility.
/// </summary>
public record CosmeticLoadoutDto(
    EquippedCosmeticDto? Frame = null,
    EquippedCosmeticDto? Trail = null,
    EquippedCosmeticDto? AvatarGear = null,
    EquippedCosmeticDto? AnswerEffect = null,
    EquippedCosmeticDto? ProfileBackground = null,
    IReadOnlyList<RareUnlockDto>? RecentRareUnlocks = null,
    long LoadoutVersion = 1,
    DateTime? LastUpdatedUtc = null
);

/// <summary>
/// Minimal cosmetic info for reward/unlock display.
/// </summary>
public record CosmeticDisplayDto(
    int Id,
    string Key,
    string Name,
    string Category,
    string Rarity,
    string AssetPath,
    string? PreviewAssetPath = null
);
