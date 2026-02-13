using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class PowerupEndpoints
{
    private const int StreakFreezeCost = 50;
    private const int MaxStreakFreezes = 5;

    public static void MapPowerupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/powerups")
            .RequireAuthorization()
            .WithTags("Powerups");

        // ❄️ BUY STREAK FREEZE
        group.MapPost("/streak-freeze/buy", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
                return Results.NotFound(new { error = "Profile not found" });

            if (profile.StreakFreezeCount >= MaxStreakFreezes)
            {
                return Results.Json(new
                {
                    error = "Max streak freezes reached",
                    max = MaxStreakFreezes,
                    current = profile.StreakFreezeCount
                }, statusCode: 409);
            }

            if (profile.Coins < StreakFreezeCost)
            {
                return Results.Json(new
                {
                    error = "Insufficient coins",
                    required = StreakFreezeCost,
                    current = profile.Coins
                }, statusCode: 402);
            }

            profile.Coins -= StreakFreezeCost;
            profile.TotalCoinsSpent += StreakFreezeCost;
            profile.StreakFreezeCount++;
            profile.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                coins = profile.Coins,
                streakFreezeCount = profile.StreakFreezeCount,
                cost = StreakFreezeCost,
                max = MaxStreakFreezes
            });
        })
        .WithName("BuyStreakFreeze");
    }
}
