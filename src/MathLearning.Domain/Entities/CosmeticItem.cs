namespace MathLearning.Domain.Entities;

/// <summary>
/// A cosmetic item that can be unlocked and equipped by users.
/// Categories: skin, hair, clothing, accessory, emoji, frame, background, effect, leaderboard_decoration
/// </summary>
public class CosmeticItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;        // skin, hair, clothing, accessory, emoji, frame, background, effect
    public string Rarity { get; set; } = "common";              // common, rare, epic, legendary, mythic
    public string AssetPath { get; set; } = string.Empty;       // e.g. "cosmetics/hair/spiky_blue.png"
    public string? PreviewAssetPath { get; set; }               // thumbnail for shop/inventory
    public string UnlockType { get; set; } = "default";         // default, xp_milestone, level, badge, leaderboard, streak, season, shop, challenge
    public string? UnlockCondition { get; set; }                // JSON or simple value, e.g. "level:5" or "streak:30"
    public int? CoinPrice { get; set; }                         // null = not purchasable with coins
    public int? SeasonId { get; set; }                          // null = permanent item
    public bool IsDefault { get; set; }                         // true = every user owns this from the start
    public DateTime? ReleaseDate { get; set; }
    public DateTime? RetirementDate { get; set; }               // null = never retired
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public CosmeticSeason? Season { get; set; }
}

/// <summary>
/// A cosmetic season with time-limited exclusive items.
/// </summary>
public class CosmeticSeason
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;            // "Math Olympiad Season", "Winter Festival"
    public string? Description { get; set; }
    public string? ThemeAssetPath { get; set; }                 // season banner/theme asset
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<CosmeticItem> Items { get; set; } = new();
}

/// <summary>
/// Tracks which cosmetics a user has unlocked.
/// </summary>
public class UserCosmeticInventory
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CosmeticItemId { get; set; }
    public string Source { get; set; } = string.Empty;          // "default", "shop", "xp_milestone", "level_up", "badge", "leaderboard", "streak", "season", "challenge"
    public int? SeasonId { get; set; }
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public CosmeticItem CosmeticItem { get; set; } = null!;
}

/// <summary>
/// The user's currently equipped avatar configuration.
/// Each slot references a CosmeticItem the user owns.
/// </summary>
public class UserAvatarConfig
{
    public string UserId { get; set; } = string.Empty;          // PK, 1:1 with UserProfile
    public int? SkinId { get; set; }
    public int? HairId { get; set; }
    public int? ClothingId { get; set; }
    public int? AccessoryId { get; set; }
    public int? EmojiId { get; set; }
    public int? FrameId { get; set; }
    public int? BackgroundId { get; set; }
    public int? EffectId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public CosmeticItem? Skin { get; set; }
    public CosmeticItem? Hair { get; set; }
    public CosmeticItem? Clothing { get; set; }
    public CosmeticItem? Accessory { get; set; }
    public CosmeticItem? Emoji { get; set; }
    public CosmeticItem? Frame { get; set; }
    public CosmeticItem? Background { get; set; }
    public CosmeticItem? Effect { get; set; }
}
