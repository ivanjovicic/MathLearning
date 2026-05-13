using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MathLearning.Infrastructure.Services;

/// <summary>
/// Builds and maintains UserCosmeticLoadoutProjection for efficient leaderboard/profile reads.
/// Denormalizes rich cosmetic metadata (names, rarities, unlock sources) into JSONB columns.
/// This prevents heavy joins during leaderboard queries and ensures frontend has all metadata.
/// </summary>
public class CosmeticLoadoutProjectionService
{
    private readonly ApiDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CosmeticLoadoutProjectionService(ApiDbContext db)
    {
        _db = db;
    }

    public async Task UpdateAfterEquipAsync(string userId)
    {
        await RebuildForUserAsync(userId);
    }

    public async Task UpdateAfterUnlockAsync(string userId)
    {
        await RebuildForUserAsync(userId);
    }

    public async Task RebuildForUserAsync(string userId)
    {
        await RebuildBatchAsync(new[] { userId });
    }

    /// <summary>
    /// Rebuilds loadout projection for multiple users.
    /// Joins with cosmetic_items to fetch metadata, then denormalizes into JSON columns.
    /// This join happens infrequently (on equip/unlock) not on every leaderboard read.
    /// </summary>
    public async Task RebuildBatchAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (!ids.Any()) return;

        // Load avatar configs (slot assignments)
        var avatars = await _db.UserAvatarConfigs
            .Where(a => ids.Contains(a.UserId))
            .ToDictionaryAsync(a => a.UserId);

        // Load inventory with cosmetic details for metadata enrichment
        var inventoriesQuery = await _db.UserCosmeticInventories
            .Include(i => i.CosmeticItem)
            .Where(i => ids.Contains(i.UserId) && !i.IsRevoked)
            .ToListAsync();

        var inventoriesByUser = inventoriesQuery
            .GroupBy(i => i.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load existing projections for updates
        var existingProjections = await _db.UserCosmeticLoadoutProjections
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId);

        foreach (var userId in ids)
        {
            var avatar = avatars.TryGetValue(userId, out var av) ? av : null;
            var inventory = inventoriesByUser.TryGetValue(userId, out var inv) ? inv : new List<UserCosmeticInventory>();

            // Ensure projection row exists
            if (!existingProjections.TryGetValue(userId, out var projection))
            {
                projection = new UserCosmeticLoadoutProjection { UserId = userId };
                _db.UserCosmeticLoadoutProjections.Add(projection);
            }

            // Helper to build rich metadata for an equipped cosmetic
            EquippedCosmeticDto? BuildEquippedCosmeticDto(int? itemId, string expectedCategory)
            {
                if (itemId == null) return null;

                var owned = inventory.FirstOrDefault(i => i.CosmeticItemId == itemId.Value);
                if (owned?.CosmeticItem == null || owned.CosmeticItem.Category != expectedCategory)
                    return null;

                return new EquippedCosmeticDto(
                    ItemId: itemId.Value,
                    Key: owned.CosmeticItem.Key,
                    Name: owned.CosmeticItem.Name,
                    Category: owned.CosmeticItem.Category,
                    Rarity: owned.CosmeticItem.Rarity,
                    UnlockSource: owned.CosmeticItem.UnlockType,
                    AssetPath: owned.CosmeticItem.AssetPath,
                    PreviewAssetPath: owned.CosmeticItem.PreviewAssetPath,
                    IsDefault: owned.CosmeticItem.IsDefault,
                    CompatibilityInfo: owned.CosmeticItem.CompatibilityRulesJson);
            }

            // Build rich metadata for each slot
            var frameDto = BuildEquippedCosmeticDto(avatar?.FrameId, CosmeticCategories.Frame);
            var trailDto = BuildEquippedCosmeticDto(avatar?.EffectId, CosmeticCategories.Effect);
            var gearDto = BuildEquippedCosmeticDto(avatar?.AccessoryId, CosmeticCategories.Accessory);
            var backgroundDto = BuildEquippedCosmeticDto(avatar?.BackgroundId, CosmeticCategories.Background);

            // Build rare unlocks list for profile display
            var rareUnlocks = inventory
                .Where(i => i.CosmeticItem.Rarity is "epic" or "legendary" or "mythic")
                .OrderByDescending(i => i.UnlockedAt)
                .Take(3)
                .Select(i => new RareUnlockDto(
                    i.CosmeticItem.Id,
                    i.CosmeticItem.Key,
                    i.CosmeticItem.Category,
                    i.CosmeticItem.Rarity,
                    i.UnlockedAt))
                .ToList();

            // Update projection with backward-compatible IDs and rich JSON metadata
            projection.AvatarFrameId = avatar?.FrameId;
            projection.TrailId = avatar?.EffectId;
            projection.AvatarGearId = avatar?.AccessoryId;
            projection.AnswerEffectId = null;
            projection.ProfileBackgroundId = avatar?.BackgroundId;

            // Store rich metadata as JSON for efficient retrieval without joins
            projection.FrameJson = frameDto != null ? JsonSerializer.Serialize(frameDto, JsonOptions) : null;
            projection.TrailJson = trailDto != null ? JsonSerializer.Serialize(trailDto, JsonOptions) : null;
            projection.AvatarGearJson = gearDto != null ? JsonSerializer.Serialize(gearDto, JsonOptions) : null;
            projection.AnswerEffectJson = null;
            projection.ProfileBackgroundJson = backgroundDto != null ? JsonSerializer.Serialize(backgroundDto, JsonOptions) : null;

            projection.RecentRareUnlocksJson = rareUnlocks.Count > 0 
                ? JsonSerializer.Serialize(rareUnlocks, JsonOptions) 
                : null;
            
            projection.LoadoutVersion = (avatar?.Version ?? 1) + 1;
            projection.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Retrieves loadout projections for multiple users.
    /// Used in leaderboard/profile queries - no joins, just JSONB deserialization.
    /// </summary>
    public async Task<Dictionary<string, UserCosmeticLoadoutProjection>> GetLoadoutsAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (!ids.Any()) return new Dictionary<string, UserCosmeticLoadoutProjection>();

        return await _db.UserCosmeticLoadoutProjections
            .AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId);
    }

    /// <summary>
    /// Deserialization helper for getting rich loadout from projection.
    /// </summary>
    public static CosmeticLoadoutDto DeserializeLoadout(UserCosmeticLoadoutProjection projection)
    {
        return new CosmeticLoadoutDto(
            Frame: projection.FrameJson != null 
                ? JsonSerializer.Deserialize<EquippedCosmeticDto>(projection.FrameJson, JsonOptions) 
                : null,
            Trail: projection.TrailJson != null 
                ? JsonSerializer.Deserialize<EquippedCosmeticDto>(projection.TrailJson, JsonOptions) 
                : null,
            AvatarGear: projection.AvatarGearJson != null 
                ? JsonSerializer.Deserialize<EquippedCosmeticDto>(projection.AvatarGearJson, JsonOptions) 
                : null,
            AnswerEffect: projection.AnswerEffectJson != null 
                ? JsonSerializer.Deserialize<EquippedCosmeticDto>(projection.AnswerEffectJson, JsonOptions) 
                : null,
            ProfileBackground: projection.ProfileBackgroundJson != null 
                ? JsonSerializer.Deserialize<EquippedCosmeticDto>(projection.ProfileBackgroundJson, JsonOptions) 
                : null,
            RecentRareUnlocks: projection.RecentRareUnlocksJson != null 
                ? JsonSerializer.Deserialize<List<RareUnlockDto>>(projection.RecentRareUnlocksJson, JsonOptions) 
                : null,
            LoadoutVersion: projection.LoadoutVersion,
            LastUpdatedUtc: projection.UpdatedAtUtc);
    }
}

