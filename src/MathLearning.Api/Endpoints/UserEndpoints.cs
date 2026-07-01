using MathLearning.Application.DTOs.Auth;
using MathLearning.Application.Services;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.DTOs.Users;
using MathLearning.Api.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class UserEndpoints
{
    private static readonly HashSet<string> SupportedLanguageCodes = new(
        ["en", "sr", "de", "es"],
        StringComparer.OrdinalIgnoreCase);

    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
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

        legacyGroup.MapGet("/profile/{userId}", async (
            ApiDbContext db,
            IAvatarAppearanceReader appearanceReader,
            string userId) => await GetProfileByIdAsync(db, appearanceReader, userId))
        .WithName("AdminGetUserProfileById")
        .WithDescription("Admin: Get user XP/level/streak by userId");

        // Mobile compatibility alias. Canonical route remains: GET /api/user/profile/{userId}
        group.MapGet("/{userId}/profile", async (
            ApiDbContext db,
            IAvatarAppearanceReader appearanceReader,
            string userId) => await GetProfileByIdAsync(db, appearanceReader, userId))
        .WithName("GetUserProfileByIdCompatibilityAlias")
        .WithSummary("Mobile compatibility alias for GET /api/user/profile/{userId}");

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
        legacyGroup.MapGet("/daily-hints", GetDailyHints);
        legacyGroup.MapGet("/hints/daily", GetDailyHints);

        group.MapGet("/profile", async (
            ApiDbContext db,
            IAvatarAppearanceReader appearanceReader,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return Results.NotFound(new { error = "Profile not found" });
            }

            var appearance = await appearanceReader.GetAppearanceAsync(userId, ctx.RequestAborted);
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
                FacultyName: profile.FacultyName,
                AvatarUrl: profile.AvatarUrl,
                Appearance: appearance
            ));
        })
        .WithName("GetUserProfile")
        .WithDescription("Get current user's profile");

        group.MapPut("/profile", async (
            UpdateProfileRequest request,
            ApiDbContext db,
            IAvatarAppearanceReader appearanceReader,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return Results.NotFound(new { error = "Profile not found" });
            }

            if (!string.IsNullOrWhiteSpace(request.DisplayName))
            {
                profile.DisplayName = request.DisplayName;
                profile.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            var appearance = await appearanceReader.GetAppearanceAsync(userId, ctx.RequestAborted);
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
                FacultyName: profile.FacultyName,
                AvatarUrl: profile.AvatarUrl,
                Appearance: appearance
            ));
        })
        .WithName("UpdateUserProfile")
        .WithDescription("Update user profile");

        group.MapGet("/stats", async (
            ApiDbContext db,
            IAvatarAppearanceReader appearanceReader,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return Results.NotFound(new { error = "Profile not found" });
            }

            var questionStats = await db.UserQuestionStats
                .Where(s => s.UserId == userId)
                .ToListAsync();

            var totalQuestions = questionStats.Count;
            var totalAttempts = questionStats.Sum(s => s.Attempts);
            var totalCorrect = questionStats.Sum(s => s.CorrectAttempts);
            var accuracy = totalAttempts > 0
                ? Math.Round((double)totalCorrect / totalAttempts * 100, 2)
                : 0;

            var hintsUsed = await db.UserHints
                .Where(h => h.UserId == userId)
                .CountAsync();
            var appearance = await appearanceReader.GetAppearanceAsync(userId, ctx.RequestAborted);

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
                    FacultyName: profile.FacultyName,
                    AvatarUrl: profile.AvatarUrl,
                    Appearance: appearance
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

        group.MapGet("/search", async (
            string query,
            ApiDbContext db,
            IAvatarAppearanceReader appearanceReader,
            CancellationToken ct,
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
                .Select(p => new PublicUserSearchResultDto(
                    p.UserId,
                    p.DisplayName ?? p.Username,
                    p.Level,
                    null,
                    null))
                .ToListAsync(ct);

            var appearanceMap = await appearanceReader.GetAppearancesAsync(
                users.Select(x => x.UserId).ToList(),
                ct);

            var result = users.Select(x => x with
            {
                Appearance = appearanceMap.TryGetValue(x.UserId, out var appearance) ? appearance : null
            });

            return Results.Ok(result);
        })
        .WithName("SearchUsers")
        .WithDescription("Search users by username or display name");

        settingsGroup.MapGet("/{userId}/settings", async (
            string userId,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            var authenticatedUserId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(authenticatedUserId))
                return Results.Unauthorized();
            if (!string.Equals(userId, authenticatedUserId, StringComparison.Ordinal))
                return Results.Forbid();

            var settings = await db.UserSettings
                .FirstOrDefaultAsync(s => s.UserId == authenticatedUserId);

            if (settings == null)
            {
                settings = new UserSettings
                {
                    UserId = authenticatedUserId,
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
                settings.LanguageCode,
                settings.Theme,
                settings.HintsEnabled,
                settings.SoundEnabled,
                settings.VibrationEnabled,
                settings.DailyNotificationEnabled,
                settings.DailyNotificationTime
            ));
        });

        settingsGroup.MapPatch("/{userId}/settings", async (
            string userId,
            UpdateUserSettingsRequest request,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            var authenticatedUserId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(authenticatedUserId))
                return Results.Unauthorized();
            if (!string.Equals(userId, authenticatedUserId, StringComparison.Ordinal))
                return Results.Forbid();

            var settings = await db.UserSettings
                .FirstOrDefaultAsync(s => s.UserId == authenticatedUserId);

            if (settings == null)
            {
                settings = new UserSettings
                {
                    UserId = authenticatedUserId,
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

            var requestedLanguageCode = request.ResolveLanguageCode();
            if (!string.IsNullOrWhiteSpace(requestedLanguageCode))
            {
                if (!SupportedLanguageCodes.Contains(requestedLanguageCode))
                {
                    return Results.BadRequest(new
                    {
                        error = "Invalid languageCode. Supported values: en, sr, de, es."
                    });
                }

                settings.LanguageCode = requestedLanguageCode.ToLowerInvariant();
            }

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
                settings.LanguageCode,
                settings.Theme,
                settings.HintsEnabled,
                settings.SoundEnabled,
                settings.VibrationEnabled,
                settings.DailyNotificationEnabled,
                settings.DailyNotificationTime
            ));
        });

        settingsGroup.MapPost("/{id:int}/avatar", async (
            int id,
            HttpRequest request,
            ApiDbContext db,
            IWebHostEnvironment env,
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
            if (file == null)
                return Results.BadRequest(new { error = "No file uploaded" });

            var validation = await LegacyAvatarUploadValidator.ValidateAsync(file);
            if (!validation.Success)
                return Results.BadRequest(new { error = validation.Error });

            var uploadsRoot = Path.Combine(env.ContentRootPath, "uploads", "avatars");
            Directory.CreateDirectory(uploadsRoot);

            var fileName = LegacyAvatarUploadValidator.BuildStorageFileName(userId, validation.NormalizedExtension);
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

        settingsGroup.MapGet("/{id:int}/avatar/{fileName}", async (
            int id,
            string fileName,
            ApiDbContext db,
            IWebHostEnvironment env,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            if (id.ToString() != userId)
                return Results.Forbid();

            if (!LegacyAvatarUploadValidator.IsSafeFileName(userId, fileName))
                return Results.NotFound(new { error = "Avatar file not found" });

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null || string.IsNullOrWhiteSpace(profile.AvatarUrl))
                return Results.NotFound(new { error = "Avatar not found" });

            if (!profile.AvatarUrl.EndsWith($"/{fileName}", StringComparison.OrdinalIgnoreCase))
                return Results.NotFound(new { error = "Avatar file not found" });

            var filePath = Path.Combine(env.ContentRootPath, "uploads", "avatars", fileName);

            if (!File.Exists(filePath))
                return Results.NotFound(new { error = "Avatar file not found" });

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(filePath, out var contentType))
                contentType = "application/octet-stream";

            return Results.File(File.OpenRead(filePath), contentType);
        });
    }

private static async Task<IResult> GetDailyHints(ApiDbContext db, HttpContext ctx)
{
    var userId = ctx.User.FindFirst("userId")?.Value;
    if (string.IsNullOrWhiteSpace(userId))
        return Results.Unauthorized();

    var today = DateTime.UtcNow.Date;
    var tomorrow = today.AddDays(1);

    var usedToday = await db.UserHints
        .AsNoTracking()
        .Where(h => h.UserId == userId && h.UsedAt >= today && h.UsedAt < tomorrow)
        .CountAsync();

    const int dailyLimit = 10;

    return Results.Ok(new
    {
        usedToday,
        dailyLimit,
        remaining = Math.Max(0, dailyLimit - usedToday)
    });
}

    private static async Task<IResult> GetProfileByIdAsync(
        ApiDbContext db,
        IAvatarAppearanceReader appearanceReader,
        string userId)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            return Results.NotFound(new { error = "Profile not found" });
        }

        var appearance = await appearanceReader.GetAppearanceAsync(userId);
        return Results.Ok(new PublicUserProfileDto(
            UserId: profile.UserId,
            DisplayName: profile.DisplayName ?? profile.Username,
            Level: profile.Level,
            Streak: profile.Streak,
            AvatarUrl: profile.AvatarUrl,
            Appearance: appearance));
    }
}

public record UpdateProfileRequest(
    string? DisplayName
);
