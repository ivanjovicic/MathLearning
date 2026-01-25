using MathLearning.Application.DTOs.Auth;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
                       .RequireAuthorization()
                       .WithTags("Users");

        // 👤 GET PROFILE
        group.MapGet("/profile", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return Results.NotFound(new { error = "Profile not found" });
            }

            return Results.Ok(new UserProfileDto(
                UserId: profile.UserId,
                Username: profile.Username,
                DisplayName: profile.DisplayName,
                Coins: profile.Coins,
                Level: profile.Level,
                Xp: profile.Xp,
                Streak: profile.Streak,
                CreatedAt: profile.CreatedAt
            ));
        })
        .WithName("GetUserProfile")
        .WithDescription("Get current user's profile");

        // ✏️ UPDATE PROFILE
        group.MapPut("/profile", async (
            UpdateProfileRequest request,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return Results.NotFound(new { error = "Profile not found" });
            }

            // Update display name
            if (!string.IsNullOrWhiteSpace(request.DisplayName))
            {
                profile.DisplayName = request.DisplayName;
                profile.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            return Results.Ok(new UserProfileDto(
                UserId: profile.UserId,
                Username: profile.Username,
                DisplayName: profile.DisplayName,
                Coins: profile.Coins,
                Level: profile.Level,
                Xp: profile.Xp,
                Streak: profile.Streak,
                CreatedAt: profile.CreatedAt
            ));
        })
        .WithName("UpdateUserProfile")
        .WithDescription("Update user profile");

        // 📊 GET STATS
        group.MapGet("/stats", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return Results.NotFound(new { error = "Profile not found" });
            }

            // Get question stats
            var questionStats = await db.UserQuestionStats
                .Where(s => s.UserId == userId)
                .ToListAsync();

            var totalQuestions = questionStats.Count;
            var totalAttempts = questionStats.Sum(s => s.Attempts);
            var totalCorrect = questionStats.Sum(s => s.CorrectAttempts);
            var accuracy = totalAttempts > 0 
                ? Math.Round((double)totalCorrect / totalAttempts * 100, 2) 
                : 0;

            // Get hint usage
            var hintsUsed = await db.UserHints
                .Where(h => h.UserId == userId)
                .CountAsync();

            return Results.Ok(new
            {
                profile = new UserProfileDto(
                    UserId: profile.UserId,
                    Username: profile.Username,
                    DisplayName: profile.DisplayName,
                    Coins: profile.Coins,
                    Level: profile.Level,
                    Xp: profile.Xp,
                    Streak: profile.Streak,
                    CreatedAt: profile.CreatedAt
                ),
                stats = new
                {
                    totalQuestions,
                    totalAttempts,
                    totalCorrect,
                    accuracy,
                    hintsUsed,
                    coins = new
                    {
                        current = profile.Coins,
                        earned = profile.TotalCoinsEarned,
                        spent = profile.TotalCoinsSpent
                    }
                }
            });
        })
        .WithName("GetUserStats")
        .WithDescription("Get user statistics");

        // 🔍 SEARCH USERS (for friend system)
        group.MapGet("/search", async (
            string query,
            ApiDbContext db,
            int limit = 10) =>
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Results.BadRequest(new { error = "Query must be at least 2 characters" });
            }

            var users = await db.UserProfiles
                .Where(p => p.Username.Contains(query) || 
                           (p.DisplayName != null && p.DisplayName.Contains(query)))
                .OrderBy(p => p.Username)
                .Take(limit)
                .Select(p => new
                {
                    p.UserId,
                    p.Username,
                    p.DisplayName,
                    p.Level,
                    p.Xp
                })
                .ToListAsync();

            return Results.Ok(users);
        })
        .WithName("SearchUsers")
        .WithDescription("Search users by username or display name");
    }
}

public record UpdateProfileRequest(
    string? DisplayName
);
