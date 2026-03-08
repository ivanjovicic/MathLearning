using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class AvatarEndpoints
{
    private static readonly HashSet<string> ValidSlots = new(StringComparer.OrdinalIgnoreCase)
    {
        "skin", "hair", "clothing", "accessory", "emoji", "frame", "background", "effect"
    };

    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "skin", "hair", "clothing", "accessory", "emoji", "frame", "background", "effect", "leaderboard_decoration"
    };

    public static void MapAvatarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cosmetics")
                       .RequireAuthorization()
                       .WithTags("Cosmetics & Avatar");

        // ─── 🛍️ GET ALL COSMETIC ITEMS (with ownership flag) ───
        group.MapGet("/items", async (
            ApiDbContext db,
            HttpContext ctx,
            string? category,
            string? rarity,
            int? seasonId) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var query = db.CosmeticItems.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(c => c.Category == category);
            if (!string.IsNullOrWhiteSpace(rarity))
                query = query.Where(c => c.Rarity == rarity);
            if (seasonId.HasValue)
                query = query.Where(c => c.SeasonId == seasonId.Value);

            // Only show items that are released and not retired
            var now = DateTime.UtcNow;
            query = query.Where(c =>
                (c.ReleaseDate == null || c.ReleaseDate <= now) &&
                (c.RetirementDate == null || c.RetirementDate > now));

            var ownedItemIds = (await db.UserCosmeticInventories
                .Where(i => i.UserId == userId)
                .Select(i => i.CosmeticItemId)
                .ToListAsync()).ToHashSet();

            var items = await query
                .OrderBy(c => c.Category)
                .ThenBy(c => c.Rarity)
                .ThenBy(c => c.Name)
                .Select(c => new CosmeticItemDto(
                    c.Id, c.Name, c.Category, c.Rarity,
                    c.AssetPath, c.PreviewAssetPath,
                    c.UnlockType, c.UnlockCondition, c.CoinPrice,
                    c.SeasonId, c.IsDefault,
                    false // placeholder — set below
                ))
                .ToListAsync();

            // Set ownership flag
            var result = items.Select(i => i with { IsOwned = ownedItemIds.Contains(i.Id) || i.IsDefault }).ToList();

            return Results.Ok(result);
        })
        .WithName("GetCosmeticItems")
        .WithDescription("List all available cosmetic items with ownership status");

        // ─── 🎒 GET USER INVENTORY ───
        group.MapGet("/inventory", async (
            ApiDbContext db,
            HttpContext ctx,
            string? category) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var query = db.UserCosmeticInventories
                .AsNoTracking()
                .Where(i => i.UserId == userId)
                .Include(i => i.CosmeticItem)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(i => i.CosmeticItem.Category == category);

            var inventory = await query
                .OrderByDescending(i => i.UnlockedAt)
                .Select(i => new InventoryItemDto(
                    i.CosmeticItemId,
                    i.CosmeticItem.Name,
                    i.CosmeticItem.Category,
                    i.CosmeticItem.Rarity,
                    i.CosmeticItem.AssetPath,
                    i.CosmeticItem.PreviewAssetPath,
                    i.Source,
                    i.UnlockedAt
                ))
                .ToListAsync();

            // Also include default items not yet in inventory
            var ownedIds = inventory.Select(i => i.CosmeticItemId).ToHashSet();
            var defaultQuery = db.CosmeticItems.AsNoTracking().Where(c => c.IsDefault && !ownedIds.Contains(c.Id));
            if (!string.IsNullOrWhiteSpace(category))
                defaultQuery = defaultQuery.Where(c => c.Category == category);

            var defaults = await defaultQuery
                .Select(c => new InventoryItemDto(
                    c.Id, c.Name, c.Category, c.Rarity,
                    c.AssetPath, c.PreviewAssetPath,
                    "default", DateTime.MinValue
                ))
                .ToListAsync();

            return Results.Ok(inventory.Concat(defaults).ToList());
        })
        .WithName("GetCosmeticInventory")
        .WithDescription("Get all cosmetics owned by the current user");

        // ─── 🎭 GET AVATAR CONFIG ───
        group.MapGet("/avatar", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var config = await db.UserAvatarConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (config == null)
            {
                return Results.Ok(new AvatarConfigDto(null, null, null, null, null, null, null, null));
            }

            return Results.Ok(new AvatarConfigDto(
                config.SkinId, config.HairId, config.ClothingId, config.AccessoryId,
                config.EmojiId, config.FrameId, config.BackgroundId, config.EffectId
            ));
        })
        .WithName("GetAvatarConfig")
        .WithDescription("Get user's currently equipped avatar configuration");

        // ─── 🎭 UPDATE FULL AVATAR CONFIG ───
        group.MapPut("/avatar", async (
            UpdateAvatarConfigRequest request,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            // Collect all non-null item IDs from the request
            var requestedIds = new List<(string slot, int? id)>
            {
                ("skin", request.SkinId), ("hair", request.HairId),
                ("clothing", request.ClothingId), ("accessory", request.AccessoryId),
                ("emoji", request.EmojiId), ("frame", request.FrameId),
                ("background", request.BackgroundId), ("effect", request.EffectId)
            };

            var nonNullIds = requestedIds.Where(r => r.id.HasValue).Select(r => r.id!.Value).ToList();

            // Validate ownership
            if (nonNullIds.Count > 0)
            {
                var validationError = await ValidateOwnership(db, userId, nonNullIds, requestedIds);
                if (validationError != null) return validationError;
            }

            var config = await db.UserAvatarConfigs.FirstOrDefaultAsync(c => c.UserId == userId);
            if (config == null)
            {
                config = new UserAvatarConfig { UserId = userId };
                db.UserAvatarConfigs.Add(config);
            }

            config.SkinId = request.SkinId;
            config.HairId = request.HairId;
            config.ClothingId = request.ClothingId;
            config.AccessoryId = request.AccessoryId;
            config.EmojiId = request.EmojiId;
            config.FrameId = request.FrameId;
            config.BackgroundId = request.BackgroundId;
            config.EffectId = request.EffectId;
            config.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new AvatarConfigDto(
                config.SkinId, config.HairId, config.ClothingId, config.AccessoryId,
                config.EmojiId, config.FrameId, config.BackgroundId, config.EffectId
            ));
        })
        .WithName("UpdateAvatarConfig")
        .WithDescription("Update user's full avatar configuration");

        // ─── 🎯 EQUIP SINGLE SLOT ───
        group.MapPost("/equip", async (
            EquipCosmeticRequest request,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            var slot = request.Slot?.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(slot) || !ValidSlots.Contains(slot))
                return Results.BadRequest(new { error = $"Invalid slot. Must be one of: {string.Join(", ", ValidSlots)}" });

            // Validate ownership if equipping (not unequipping)
            if (request.CosmeticItemId.HasValue)
            {
                var itemId = request.CosmeticItemId.Value;
                var item = await db.CosmeticItems.AsNoTracking().FirstOrDefaultAsync(c => c.Id == itemId);
                if (item == null)
                    return Results.NotFound(new { error = "Cosmetic item not found" });
                if (!string.Equals(item.Category, slot, StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest(new { error = $"Item category '{item.Category}' does not match slot '{slot}'" });
                if (!item.IsDefault)
                {
                    var owned = await db.UserCosmeticInventories
                        .AnyAsync(i => i.UserId == userId && i.CosmeticItemId == itemId);
                    if (!owned)
                        return Results.Json(new { error = "You do not own this item" }, statusCode: 403);
                }
            }

            var config = await db.UserAvatarConfigs.FirstOrDefaultAsync(c => c.UserId == userId);
            if (config == null)
            {
                config = new UserAvatarConfig { UserId = userId };
                db.UserAvatarConfigs.Add(config);
            }

            SetSlot(config, slot, request.CosmeticItemId);
            config.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new AvatarConfigDto(
                config.SkinId, config.HairId, config.ClothingId, config.AccessoryId,
                config.EmojiId, config.FrameId, config.BackgroundId, config.EffectId
            ));
        })
        .WithName("EquipCosmetic")
        .WithDescription("Equip or unequip a single cosmetic slot");

        // ─── 💰 PURCHASE COSMETIC WITH COINS ───
        group.MapPost("/purchase", async (
            PurchaseCosmeticRequest request,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var item = await db.CosmeticItems.FirstOrDefaultAsync(c => c.Id == request.CosmeticItemId);
            if (item == null)
                return Results.NotFound(new { error = "Cosmetic item not found" });

            if (!item.CoinPrice.HasValue || item.CoinPrice.Value <= 0)
                return Results.BadRequest(new { error = "This item cannot be purchased with coins" });

            // Check if retired
            if (item.RetirementDate.HasValue && item.RetirementDate.Value <= DateTime.UtcNow)
                return Results.BadRequest(new { error = "This item is no longer available" });

            // Check if already owned
            var alreadyOwned = await db.UserCosmeticInventories
                .AnyAsync(i => i.UserId == userId && i.CosmeticItemId == item.Id);
            if (alreadyOwned)
                return Results.Conflict(new { error = "You already own this item" });

            // Check coins
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
                return Results.NotFound(new { error = "Profile not found" });
            if (profile.Coins < item.CoinPrice.Value)
                return Results.Json(new { error = "Insufficient coins", required = item.CoinPrice.Value, current = profile.Coins }, statusCode: 402);

            // Deduct coins
            profile.Coins -= item.CoinPrice.Value;
            profile.TotalCoinsSpent += item.CoinPrice.Value;
            profile.UpdatedAt = DateTime.UtcNow;

            // Add to inventory
            db.UserCosmeticInventories.Add(new UserCosmeticInventory
            {
                UserId = userId,
                CosmeticItemId = item.Id,
                Source = "shop",
                SeasonId = item.SeasonId,
                UnlockedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            return Results.Ok(new PurchaseCosmeticResponse(true, $"Purchased '{item.Name}' for {item.CoinPrice.Value} coins", profile.Coins));
        })
        .WithName("PurchaseCosmetic")
        .WithDescription("Purchase a cosmetic item with coins");

        // ─── 📅 GET SEASONS ───
        group.MapGet("/seasons", async (
            ApiDbContext db,
            bool? activeOnly) =>
        {
            var query = db.CosmeticSeasons.AsNoTracking().AsQueryable();
            if (activeOnly == true)
                query = query.Where(s => s.IsActive);

            var seasons = await query
                .OrderByDescending(s => s.StartDate)
                .Select(s => new CosmeticSeasonDto(
                    s.Id, s.Name, s.Description, s.ThemeAssetPath,
                    s.StartDate, s.EndDate, s.IsActive,
                    s.Items.Count
                ))
                .ToListAsync();

            return Results.Ok(seasons);
        })
        .AllowAnonymous()
        .WithName("GetCosmeticSeasons")
        .WithDescription("List cosmetic seasons");

        // ─── 🎭 GET ANY USER'S AVATAR (public, for leaderboard/profiles) ───
        group.MapGet("/avatar/{userId}", async (
            string userId,
            ApiDbContext db) =>
        {
            var config = await db.UserAvatarConfigs
                .AsNoTracking()
                .Include(c => c.Skin)
                .Include(c => c.Hair)
                .Include(c => c.Clothing)
                .Include(c => c.Accessory)
                .Include(c => c.Emoji)
                .Include(c => c.Frame)
                .Include(c => c.Background)
                .Include(c => c.Effect)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (config == null)
                return Results.Ok(new AvatarSummaryDto(null, null, null, null, null, null, null, null));

            return Results.Ok(new AvatarSummaryDto(
                config.Skin?.AssetPath,
                config.Hair?.AssetPath,
                config.Clothing?.AssetPath,
                config.Accessory?.AssetPath,
                config.Emoji?.AssetPath,
                config.Frame?.AssetPath,
                config.Background?.AssetPath,
                config.Effect?.AssetPath
            ));
        })
        .AllowAnonymous()
        .WithName("GetUserAvatar")
        .WithDescription("Get any user's avatar for display in leaderboards/profiles");
    }

    // ─── Helpers ───

    private static void SetSlot(UserAvatarConfig config, string slot, int? itemId)
    {
        switch (slot)
        {
            case "skin": config.SkinId = itemId; break;
            case "hair": config.HairId = itemId; break;
            case "clothing": config.ClothingId = itemId; break;
            case "accessory": config.AccessoryId = itemId; break;
            case "emoji": config.EmojiId = itemId; break;
            case "frame": config.FrameId = itemId; break;
            case "background": config.BackgroundId = itemId; break;
            case "effect": config.EffectId = itemId; break;
        }
    }

    private static async Task<IResult?> ValidateOwnership(
        ApiDbContext db, string userId, List<int> nonNullIds,
        List<(string slot, int? id)> requestedIds)
    {
        var ownedIds = (await db.UserCosmeticInventories
            .Where(i => i.UserId == userId && nonNullIds.Contains(i.CosmeticItemId))
            .Select(i => i.CosmeticItemId)
            .ToListAsync()).ToHashSet();

        var defaultIds = (await db.CosmeticItems
            .Where(c => nonNullIds.Contains(c.Id) && c.IsDefault)
            .Select(c => c.Id)
            .ToListAsync()).ToHashSet();

        // Validate each requested item exists, is owned, and matches its slot category
        var allItems = await db.CosmeticItems
            .AsNoTracking()
            .Where(c => nonNullIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Category);

        foreach (var (slot, id) in requestedIds)
        {
            if (!id.HasValue) continue;
            var itemId = id.Value;

            if (!allItems.TryGetValue(itemId, out var itemCategory))
                return Results.NotFound(new { error = $"Cosmetic item {itemId} not found" });

            if (!string.Equals(itemCategory, slot, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = $"Item {itemId} (category '{itemCategory}') does not match slot '{slot}'" });

            if (!ownedIds.Contains(itemId) && !defaultIds.Contains(itemId))
                return Results.Json(new { error = $"You do not own item {itemId}" }, statusCode: 403);
        }

        return null;
    }
}
