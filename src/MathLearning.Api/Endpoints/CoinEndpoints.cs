using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class CoinEndpoints
{
    public static void MapCoinEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/coins")
                       .RequireAuthorization()
                       .WithTags("Coins");

        // 💰 GET COIN BALANCE
        group.MapGet("/balance", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                return Results.Ok(new
                {
                    coins = 0,
                    totalEarned = 0,
                    totalSpent = 0,
                    level = 1,
                    xp = 0
                });
            }

            return Results.Ok(new
            {
                coins = profile.Coins,
                totalEarned = profile.TotalCoinsEarned,
                totalSpent = profile.TotalCoinsSpent,
                level = profile.Level,
                xp = profile.Xp,
                streak = profile.Streak
            });
        })
        .WithName("GetCoinBalance");

        // 🎁 EARN COINS (called after correct answer)
        group.MapPost("/earn", async (
            ApiDbContext db,
            HttpContext ctx,
            int amount,
            string? reason = null) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                return Results.NotFound(new { error = "Profile not found" });
            }

            profile.Coins += amount;
            profile.TotalCoinsEarned += amount;
            profile.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = $"Earned {amount} coins",
                reason = reason ?? "Unknown",
                newBalance = profile.Coins,
                totalEarned = profile.TotalCoinsEarned
            });
        })
        .WithName("EarnCoins");

        // 💸 SPEND COINS (manual - usually hints handle this)
        group.MapPost("/spend", async (
            ApiDbContext db,
            HttpContext ctx,
            int amount,
            string? reason = null) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                return Results.NotFound(new { error = "Profile not found" });
            }

            if (profile.Coins < amount)
            {
                return Results.Json(new
                {
                    error = "Insufficient coins",
                    required = amount,
                    current = profile.Coins
                }, statusCode: 402);
            }

            profile.Coins -= amount;
            profile.TotalCoinsSpent += amount;
            profile.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = $"Spent {amount} coins",
                reason = reason ?? "Unknown",
                newBalance = profile.Coins,
                totalSpent = profile.TotalCoinsSpent
            });
        })
        .WithName("SpendCoins");

        // 📊 GET COIN HISTORY
        group.MapGet("/history", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            // Get hint usage (coin spending)
            var hints = await db.UserHints
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.UsedAt)
                .Take(50)
                .Select(h => new
                {
                    type = "spent",
                    amount = h.HintType == "formula" ? -5 :
                             h.HintType == "clue" ? -10 :
                             h.HintType == "eliminate" ? -15 :
                             h.HintType == "solution" ? -20 : 0,
                    reason = $"Used {h.HintType} hint",
                    timestamp = h.UsedAt
                })
                .ToListAsync();

            // TODO: Get coin earnings (from correct answers)
            // This requires tracking coin transactions separately

            return Results.Ok(hints);
        })
        .WithName("GetCoinHistory");

        // 🏆 LEADERBOARD (richest users)
        group.MapGet("/leaderboard", async (
            ApiDbContext db,
            int limit = 10) =>
        {
            var richestUsers = await db.UserProfiles
                .OrderByDescending(p => p.Coins)
                .Take(limit)
                .Select(p => new
                {
                    rank = 0, // Will be set below
                    username = p.Username,
                    coins = p.Coins,
                    level = p.Level,
                    totalEarned = p.TotalCoinsEarned,
                    totalSpent = p.TotalCoinsSpent
                })
                .ToListAsync();

            // Add ranks
            var ranked = richestUsers.Select((user, index) => new
            {
                rank = index + 1,
                user.username,
                user.coins,
                user.level,
                user.totalEarned,
                user.totalSpent
            });

            return Results.Ok(ranked);
        })
        .WithName("GetCoinLeaderboard");
    }
}
