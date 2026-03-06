using MathLearning.Application.DTOs.Auth;
using MathLearning.Application.DTOs.Users;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
            // ADMIN: Get user profile by userId
            legacyGroup.MapGet("/profile/{userId}", async (
                ApiDbContext db,
                string userId) =>
            {
                var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile == null)
                {
                    return Results.NotFound(new { error = "Profile not found" });
                }
                return Results.Ok(new
                {
                    profile.UserId,
                    profile.Username,
                    profile.Xp,
                    profile.Level,
                    profile.Streak,
                    profile.DailyXp,
                    profile.WeeklyXp,
                    profile.MonthlyXp
                });
            })
            .WithName("AdminGetUserProfileById")
            .WithDescription("Admin: Get user XP/level/streak by userId");
    {
        var group = app.MapGroup("/api/users")
                       .RequireAuthorization()
                       .WithTags("Users");
        var legacyGroup = app.MapGroup("/api/user")
                             .RequireAuthorization()
                             .WithTags("Users");

        var settingsGroup = app.MapGroup("/users")
                              .RequireAuthorization()
                              .WithTags("UserSettings");

        // Legacy mobile endpoint: GET /api/user/coins
        legacyGroup.MapGet("/coins", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var profile = await db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                return Results.Ok(new
                {
                    coins = 0,
                    totalEarned = 0,
                    totalSpent = 0,
                    level = 1,
                    xp = 0,
                    streak = 0
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
        });

        // Legacy mobile endpoint: GET /api/user/daily-hints
        legacyGroup.MapGet("/daily-hints", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var usedToday = await db.UserHints
                .AsNoTracking()
                .Where(h => h.UserId == userId && h.UsedAt >= today && h.UsedAt < tomorrow)
                .CountAsync();

            const int dailyLimit = 10;
            var remaining = Math.Max(0, dailyLimit - usedToday);

            return Results.Ok(new
            {
                usedToday,
                dailyLimit,
                remaining
            });
        });

        // 👤 GET PROFILE
        group.MapGet("/profile", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

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
                CreatedAt: profile.CreatedAt,
                SchoolName: profile.SchoolName,
                FacultyName: profile.FacultyName
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
            string userId = ctx.User.FindFirst("userId")!.Value;

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
                CreatedAt: profile.CreatedAt,
                SchoolName: profile.SchoolName,
                FacultyName: profile.FacultyName
            ));
        })
        .WithName("UpdateUserProfile")
        .WithDescription("Update user profile");

        // 📊 GET STATS
        group.MapGet("/stats", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

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
                    CreatedAt: profile.CreatedAt,
                    SchoolName: profile.SchoolName,
                    FacultyName: profile.FacultyName
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

        // GET /users/{id}/settings
        settingsGroup.MapGet("/{id:int}/settings", async (
            int id,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            if (id.ToString() != userId)
                return Results.Forbid();

            var settings = await db.UserSettings
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (settings == null)
            {
                settings = new UserSettings
                {
                    UserId = userId,
                    Language = "sr",
                    Theme = "light",
                    HintsEnabled = true,
                    SoundEnabled = true,
                    VibrationEnabled = true,
                    DailyNotificationEnabled = false,
                    DailyNotificationTime = "18:00",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.UserSettings.Add(settings);
                await db.SaveChangesAsync();
            }

            return Results.Ok(new UserSettingsDto(
                settings.UserId,
                settings.Language,
                settings.Theme,
                settings.HintsEnabled,
                settings.SoundEnabled,
                settings.VibrationEnabled,
                settings.DailyNotificationEnabled,
                settings.DailyNotificationTime
            ));
        });

        // PATCH /users/{id}/settings
        settingsGroup.MapPatch("/{id:int}/settings", async (
            int id,
            UpdateUserSettingsRequest request,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            if (id.ToString() != userId)
                return Results.Forbid();

            var settings = await db.UserSettings
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (settings == null)
            {
                settings = new UserSettings
                {
                    UserId = userId,
                    Language = "sr",
                    Theme = "light",
                    HintsEnabled = true,
                    SoundEnabled = true,
                    VibrationEnabled = true,
                    DailyNotificationEnabled = false,
                    DailyNotificationTime = "18:00",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.UserSettings.Add(settings);
            }

            if (!string.IsNullOrWhiteSpace(request.Language))
                settings.Language = request.Language;

            if (!string.IsNullOrWhiteSpace(request.Theme))
                settings.Theme = request.Theme;

            if (request.HintsEnabled.HasValue)
                settings.HintsEnabled = request.HintsEnabled.Value;

            if (request.SoundEnabled.HasValue)
                settings.SoundEnabled = request.SoundEnabled.Value;

            if (request.VibrationEnabled.HasValue)
                settings.VibrationEnabled = request.VibrationEnabled.Value;

            if (request.DailyNotificationEnabled.HasValue)
                settings.DailyNotificationEnabled = request.DailyNotificationEnabled.Value;

            if (!string.IsNullOrWhiteSpace(request.DailyNotificationTime))
                settings.DailyNotificationTime = request.DailyNotificationTime;

            settings.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new UserSettingsDto(
                settings.UserId,
                settings.Language,
                settings.Theme,
                settings.HintsEnabled,
                settings.SoundEnabled,
                settings.VibrationEnabled,
                settings.DailyNotificationEnabled,
                settings.DailyNotificationTime
            ));
        });

        // POST /users/{id}/avatar
        settingsGroup.MapPost("/{id:int}/avatar", async (
            int id,
            HttpRequest request,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            if (id.ToString() != userId)
                return Results.Forbid();

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
                return Results.NotFound(new { error = "Profile not found" });

            var form = await request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "No file uploaded" });

            var uploadsRoot = Path.Combine(AppContext.BaseDirectory, "uploads", "avatars");
            Directory.CreateDirectory(uploadsRoot);

            var fileExt = Path.GetExtension(file.FileName);
            var fileName = $"{userId}_{Guid.NewGuid():N}{fileExt}";
            var filePath = Path.Combine(uploadsRoot, fileName);

            await using (var stream = File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/users/{userId}/avatar/{fileName}";
            profile.AvatarUrl = url;
            profile.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { avatarUrl = url });
        });

        // GET /users/{id}/avatar/{fileName}
        settingsGroup.MapGet("/{id:int}/avatar/{fileName}", async (
            int id,
            string fileName,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            if (id.ToString() != userId)
                return Results.Forbid();

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null || string.IsNullOrWhiteSpace(profile.AvatarUrl))
                return Results.NotFound(new { error = "Avatar not found" });

            if (!profile.AvatarUrl.EndsWith($"/{fileName}", StringComparison.OrdinalIgnoreCase))
                return Results.NotFound(new { error = "Avatar file not found" });

            var filePath = Path.Combine(AppContext.BaseDirectory, "uploads", "avatars", fileName);

            if (!File.Exists(filePath))
                return Results.NotFound(new { error = "Avatar file not found" });

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(filePath, out var contentType))
                contentType = "application/octet-stream";

            return Results.File(File.OpenRead(filePath), contentType);
        });
    }
}

public record UpdateProfileRequest(
    string? DisplayName
);
