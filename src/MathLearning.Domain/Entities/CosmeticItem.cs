namespace MathLearning.Domain.Entities;

public static class CosmeticCategories
{
    public const string Skin = "skin";
    public const string Hair = "hair";
    public const string Clothing = "clothing";
    public const string Accessory = "accessory";
    public const string Emoji = "emoji";
    public const string Frame = "frame";
    public const string Background = "background";
    public const string Effect = "effect";
    public const string LeaderboardDecoration = "leaderboard_decoration";
}

public static class CosmeticUnlockTypes
{
    public const string Default = "default";
    public const string Shop = "shop";
    public const string XpMilestone = "xp_milestone";
    public const string Level = "level";
    public const string Badge = "badge";
    public const string Leaderboard = "leaderboard";
    public const string Streak = "streak";
    public const string Season = "season";
    public const string Challenge = "challenge";
    public const string RewardRule = "reward_rule";
    public const string AdminGrant = "admin_grant";
}

public static class CosmeticTrackTypes
{
    public const string Free = "free";
    public const string Premium = "premium";
}

public static class CosmeticSeasonStatuses
{
    public const string Draft = "draft";
    public const string Scheduled = "scheduled";
    public const string Active = "active";
    public const string RewardLock = "reward_lock";
    public const string Completed = "completed";
    public const string Archived = "archived";
}

public static class CosmeticTelemetryEventTypes
{
    public const string Unlock = "cosmetic_unlocked";
    public const string Equip = "cosmetic_equipped";
    public const string Unequip = "cosmetic_unequipped";
    public const string Purchase = "cosmetic_purchased";
    public const string RewardClaimDuplicateBlocked = "cosmetic_claim_duplicate_blocked";
    public const string RewardEvaluated = "cosmetic_reward_evaluated";
    public const string AvatarUpdated = "avatar_updated";
}

public class CosmeticItem
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Rarity { get; set; } = "common";
    public string AssetPath { get; set; } = string.Empty;
    public string? PreviewAssetPath { get; set; }
    public string UnlockType { get; set; } = CosmeticUnlockTypes.Default;
    public string? UnlockCondition { get; set; }
    public string? UnlockConditionJson { get; set; }
    public string? CompatibilityRulesJson { get; set; }
    public int? CoinPrice { get; set; }
    public int? SeasonId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsHidden { get; set; }
    public int SortOrder { get; set; }
    public string AssetVersion { get; set; } = "1";
    public DateTime? ReleaseDate { get; set; }
    public DateTime? RetirementDate { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public CosmeticSeason? Season { get; set; }
}

public class CosmeticSeason
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Theme { get; set; }
    public string? ThemeAssetPath { get; set; }
    public string Status { get; set; } = CosmeticSeasonStatuses.Draft;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? RewardLockAt { get; set; }
    public DateTime? ArchiveAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<CosmeticItem> Items { get; set; } = new();
    public List<SeasonRewardTrackEntry> RewardTrackEntries { get; set; } = new();
}

public class UserCosmeticInventory
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CosmeticItemId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? SourceRef { get; set; }
    public string? GrantReason { get; set; }
    public int? SeasonId { get; set; }
    public string AssetVersion { get; set; } = "1";
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;

    public CosmeticItem CosmeticItem { get; set; } = null!;
}

public class UserAvatarConfig
{
    public string UserId { get; set; } = string.Empty;
    public int? SkinId { get; set; }
    public int? HairId { get; set; }
    public int? ClothingId { get; set; }
    public int? AccessoryId { get; set; }
    public int? EmojiId { get; set; }
    public int? FrameId { get; set; }
    public int? BackgroundId { get; set; }
    public int? EffectId { get; set; }
    public int? LeaderboardDecorationId { get; set; }
    public long Version { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public CosmeticItem? Skin { get; set; }
    public CosmeticItem? Hair { get; set; }
    public CosmeticItem? Clothing { get; set; }
    public CosmeticItem? Accessory { get; set; }
    public CosmeticItem? Emoji { get; set; }
    public CosmeticItem? Frame { get; set; }
    public CosmeticItem? Background { get; set; }
    public CosmeticItem? Effect { get; set; }
    public CosmeticItem? LeaderboardDecoration { get; set; }
}

public class CosmeticRewardRule
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? ConditionJson { get; set; }
    public string RewardType { get; set; } = "cosmetic_item";
    public string RewardPayloadJson { get; set; } = "{}";
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CosmeticRewardClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string RewardKey { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceRef { get; set; } = string.Empty;
    public int CosmeticItemId { get; set; }
    public DateTime ClaimedAtUtc { get; set; } = DateTime.UtcNow;

    public CosmeticItem CosmeticItem { get; set; } = null!;
}

public class SeasonRewardTrackEntry
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public string TrackType { get; set; } = CosmeticTrackTypes.Free;
    public int Tier { get; set; }
    public int XpRequired { get; set; }
    public string RewardType { get; set; } = "cosmetic_item";
    public string RewardPayloadJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public CosmeticSeason Season { get; set; } = null!;
}

public class UserAppearanceProjection
{
    public string UserId { get; set; } = string.Empty;
    public long AvatarVersion { get; set; }
    public int? SkinId { get; set; }
    public int? HairId { get; set; }
    public int? ClothingId { get; set; }
    public int? AccessoryId { get; set; }
    public int? EmojiId { get; set; }
    public int? FrameId { get; set; }
    public int? BackgroundId { get; set; }
    public int? EffectId { get; set; }
    public int? LeaderboardDecorationId { get; set; }
    public string? SkinAssetPath { get; set; }
    public string? HairAssetPath { get; set; }
    public string? ClothingAssetPath { get; set; }
    public string? AccessoryAssetPath { get; set; }
    public string? EmojiAssetPath { get; set; }
    public string? FrameAssetPath { get; set; }
    public string? BackgroundAssetPath { get; set; }
    public string? EffectAssetPath { get; set; }
    public string? LeaderboardDecorationAssetPath { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CosmeticTelemetryEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int? CosmeticItemId { get; set; }
    public int? SeasonId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

public class CosmeticAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Action { get; set; } = string.Empty;
    public string? ActorUserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
