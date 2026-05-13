using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MathLearning.Infrastructure.Services;

public class CosmeticLoadoutProjectionService
{
    private readonly ApiDbContext _db;

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

    public async Task RebuildBatchAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (!ids.Any()) return;

        var avatars = await _db.UserAvatarConfigs
            .Where(a => ids.Contains(a.UserId))
            .ToDictionaryAsync(a => a.UserId);

        var inventoriesQuery = await _db.UserCosmeticInventories
            .Include(i => i.CosmeticItem)
            .Where(i => ids.Contains(i.UserId) && !i.IsRevoked)
            .ToListAsync();

        var inventoriesByUser = inventoriesQuery
            .GroupBy(i => i.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var existingProjections = await _db.UserCosmeticLoadoutProjections
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId);

        foreach (var userId in ids)
        {
            var avatar = avatars.TryGetValue(userId, out var av) ? av : null;
            var inventory = inventoriesByUser.TryGetValue(userId, out var inv) ? inv : new List<UserCosmeticInventory>();

            int? GetEquippedIfOwned(int? equippedId, string expectedCategory)
            {
                if (equippedId == null) return null;
                var owned = inventory.FirstOrDefault(i => i.CosmeticItemId == equippedId.Value);
                return owned?.CosmeticItem.Category == expectedCategory ? equippedId : null;
            }

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

            if (!existingProjections.TryGetValue(userId, out var projection))
            {
                projection = new UserCosmeticLoadoutProjection { UserId = userId };
                _db.UserCosmeticLoadoutProjections.Add(projection);
            }

            projection.AvatarFrameId = GetEquippedIfOwned(avatar?.FrameId, CosmeticCategories.Frame);
            projection.TrailId = GetEquippedIfOwned(avatar?.EffectId, CosmeticCategories.Effect);
            projection.AvatarGearId = GetEquippedIfOwned(avatar?.AccessoryId, CosmeticCategories.Accessory);
            projection.AnswerEffectId = null;
            projection.ProfileBackgroundId = GetEquippedIfOwned(avatar?.BackgroundId, CosmeticCategories.Background);
            projection.RecentRareUnlocksJson = rareUnlocks.Count > 0 ? JsonSerializer.Serialize(rareUnlocks) : null;
            projection.LoadoutVersion = avatar?.Version ?? 1;
            projection.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<Dictionary<string, UserCosmeticLoadoutProjection>> GetLoadoutsAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (!ids.Any()) return new Dictionary<string, UserCosmeticLoadoutProjection>();

        return await _db.UserCosmeticLoadoutProjections
            .AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId);
    }
}
