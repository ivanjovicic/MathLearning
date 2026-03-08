using MathLearning.Application.DTOs.Leaderboard;
using MathLearning.Application.DTOs.Progress;
using MathLearning.Application.Services;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Core.DTOs;
using MathLearning.Core.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class LeaderboardEndpoints
{
    public sealed record AddXpRequest(int Amount);

    public static void MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leaderboard")
            .RequireAuthorization()
            .WithTags("Leaderboard");

        group.MapGet("", async (
            [FromServices] IRedisLeaderboardService leaderboardService,
            [FromServices] ApiDbContext db,
            HttpContext ctx,
            string scope = "global",
            string period = "all_time",
            int limit = 50,
            string? cursor = null,
            bool includeMe = true) =>
        {
            var userId = ctx.User.FindFirst("userId")!.Value;
            var leaderboard = await leaderboardService.GetLeaderboardAsync(new LeaderboardRequestDto
            {
                Scope = scope,
                Period = period,
                Limit = limit,
                Cursor = cursor,
                UserId = userId
            });

            var userRankCore = includeMe
                ? await leaderboardService.GetUserRankAsync(new LeaderboardRequestDto { Scope = scope, Period = period, UserId = userId })
                : null;
            var appearanceMap = await LoadAppearanceMapAsync(db, leaderboard.Select(x => x.UserId), ctx.RequestAborted);

            var items = leaderboard.Select(e => new LeaderboardItemDto
            {
                Rank = e.Rank,
                UserId = e.UserId,
                DisplayName = e.DisplayName,
                AvatarUrl = null,
                Appearance = appearanceMap.TryGetValue(e.UserId, out var appearance) ? appearance : null,
                Score = e.Xp,
                StreakDays = e.Streak,
                Level = e.Level
            }).ToList();

            LeaderboardMeDto? me = null;
            if (userRankCore != null)
            {
                me = new LeaderboardMeDto
                {
                    Rank = userRankCore.Rank,
                    Score = userRankCore.Xp,
                    Percentile = 0,
                    Badges = new List<string>()
                };
            }

            return Results.Ok(new LeaderboardResponseDto
            {
                Scope = scope,
                Period = period,
                Items = items,
                Me = me,
                NextCursor = cursor
            });
        })
        .WithName("GetLeaderboard")
        .WithSummary("Get leaderboard with Redis-based queries and real-time updates");

        group.MapGet("/schools", async (
            [FromServices] ILeaderboardService leaderboardService,
            HttpContext ctx,
            string period = "week",
            int limit = 50,
            string? cursor = null) =>
        {
            var userId = ctx.User.FindFirst("userId")!.Value;
            var result = await leaderboardService.GetSchoolLeaderboardAsync(userId, period, limit, cursor);
            return Results.Ok(result);
        })
        .WithName("GetSchoolLeaderboard")
        .WithSummary("Get school vs school aggregate leaderboard");

        group.MapGet("/schools/{schoolId:int}", async (
            [FromServices] ISchoolLeaderboardService schoolLeaderboardService,
            int schoolId,
            string period = "week",
            int neighbors = 2,
            CancellationToken ct = default) =>
        {
            var result = await schoolLeaderboardService.GetSchoolLeaderboardDetailsAsync(schoolId, period, neighbors, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetSchoolLeaderboardDetail")
        .WithSummary("Get school leaderboard details and nearby ranks");

        group.MapGet("/schools/history/{schoolId:int}", async (
            [FromServices] ISchoolLeaderboardService schoolLeaderboardService,
            int schoolId,
            string period = "week",
            int take = 30,
            CancellationToken ct = default) =>
        {
            var result = await schoolLeaderboardService.GetSchoolLeaderboardHistoryAsync(schoolId, period, take, ct);
            return Results.Ok(result);
        })
        .WithName("GetSchoolLeaderboardHistory")
        .WithSummary("Get historical school rank snapshots for the active period");

        group.MapGet("/global", async (
            [FromServices] ApiDbContext db,
            HttpContext ctx,
            string range = "allTime",
            int limit = 50) =>
        {
            var userId = ctx.User.FindFirst("userId")!.Value;

            var profileDict = (await db.UserProfiles
                .Select(p => new { p.UserId, p.DisplayName, p.Username, p.Level, p.Streak })
                .ToListAsync())
                .ToDictionary(p => p.UserId);

            var globalList = await (
                from s in db.UserQuestionStats
                group s by s.UserId into g
                select new
                {
                    UserId = g.Key,
                    TotalCorrect = g.Sum(x => x.CorrectAttempts)
                }
            ).ToListAsync();

            var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.Date.DayOfWeek + 1);

            var weeklyDict = (await db.UserAnswers
                .Where(a => a.AnsweredAt >= weekStart && a.IsCorrect)
                .GroupBy(a => a.UserId)
                .Select(g => new { UserId = g.Key, WeeklyCorrect = g.Count() })
                .ToListAsync())
                .ToDictionary(x => x.UserId, x => x.WeeklyCorrect * 10);

            var enriched = globalList.Select(x =>
            {
                var xp = x.TotalCorrect * 10;
                profileDict.TryGetValue(x.UserId, out var profile);
                weeklyDict.TryGetValue(x.UserId, out var weeklyXp);

                return new
                {
                    x.UserId,
                    DisplayName = profile?.DisplayName ?? profile?.Username ?? $"User{x.UserId}",
                    Level = profile?.Level ?? (1 + xp / 100),
                    Xp = xp,
                    WeeklyXp = weeklyXp,
                    Streak = profile?.Streak ?? 0
                };
            }).ToList();

            var ordered = range.ToLowerInvariant() switch
            {
                "weekly" => enriched.OrderByDescending(x => x.WeeklyXp).ThenByDescending(x => x.Xp),
                _ => enriched.OrderByDescending(x => x.Xp)
            };

            var rankedRows = ordered.Take(limit).ToList();
            var appearanceMap = await LoadAppearanceMapAsync(db, rankedRows.Select(x => x.UserId), ctx.RequestAborted);

            var ranked = rankedRows
                .Select((x, index) => new
                {
                    rank = index + 1,
                    userId = x.UserId,
                    displayName = x.DisplayName,
                    level = x.Level,
                    xp = x.Xp,
                    weeklyXp = x.WeeklyXp,
                    streak = x.Streak,
                    appearance = appearanceMap.TryGetValue(x.UserId, out var appearance) ? appearance : null
                })
                .ToList();

            return Results.Ok(ranked);
        })
        .WithName("GetGlobalLeaderboard")
        .WithSummary("Get global leaderboard (legacy endpoint)");

        group.MapGet("/friends", async (
            [FromServices] IRedisLeaderboardService leaderboardService,
            [FromServices] ApiDbContext db,
            HttpContext ctx,
            string scope = "friends",
            string period = "weekly",
            int limit = 50) =>
        {
            var userId = ctx.User.FindFirst("userId")!.Value;
            var leaderboard = await leaderboardService.GetLeaderboardAsync(new LeaderboardRequestDto
            {
                Scope = scope,
                Period = period,
                Limit = limit,
                UserId = userId
            });
            var userRankCore = await leaderboardService.GetUserRankAsync(new LeaderboardRequestDto
            {
                Scope = scope,
                Period = period,
                UserId = userId
            });
            var appearanceMap = await LoadAppearanceMapAsync(db, leaderboard.Select(x => x.UserId), ctx.RequestAborted);

            var items = leaderboard.Select(e => new LeaderboardItemDto
            {
                Rank = e.Rank,
                UserId = e.UserId,
                DisplayName = e.DisplayName,
                AvatarUrl = null,
                Appearance = appearanceMap.TryGetValue(e.UserId, out var appearance) ? appearance : null,
                Score = e.Xp,
                StreakDays = e.Streak,
                Level = e.Level
            }).ToList();

            LeaderboardMeDto? me = null;
            if (userRankCore != null)
            {
                me = new LeaderboardMeDto
                {
                    Rank = userRankCore.Rank,
                    Score = userRankCore.Xp,
                    Percentile = 0,
                    Badges = new List<string>()
                };
            }

            return Results.Ok(new LeaderboardResponseDto
            {
                Scope = scope,
                Period = period,
                Items = items,
                Me = me,
                NextCursor = null
            });
        })
        .WithName("GetFriendsLeaderboard")
        .WithSummary("Get friends leaderboard using Redis");

        group.MapPost("/admin/add-xp/{userId}", async (
            [FromServices] XpTrackingService xpService,
            string userId,
            [FromBody] AddXpRequest req) =>
        {
            if (req.Amount == 0)
            {
                return Results.BadRequest(new { error = "Nedostaje amount" });
            }

            var profile = await xpService.AddXpAsync(userId, req.Amount);
            return Results.Ok(new { message = $"XP promenjen za korisnika {userId}", newXp = profile.Xp, newLevel = profile.Level });
        })
        .RequireAuthorization("Admin")
        .WithName("AdminAddXp")
        .WithSummary("Ručno dodeljuje XP korisniku (bonus/korekcija, admin endpoint)");

        group.MapPost("/admin/reset-xp/{userId}", async (
            [FromServices] XpTrackingService xpService,
            string userId) =>
        {
            await xpService.ResetTimeBasedXpAsync(userId);
            return Results.Ok(new { message = $"XP reset for user {userId}" });
        })
        .RequireAuthorization("Admin")
        .WithName("AdminResetXp")
        .WithSummary("Resetuje XP (daily/weekly/monthly) za korisnika (admin endpoint)");
    }

    private static async Task<Dictionary<string, AvatarAppearanceDto>> LoadAppearanceMapAsync(
        ApiDbContext db,
        IEnumerable<string> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, AvatarAppearanceDto>();
        }

        return await db.UserAppearanceProjections
            .AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, MapAppearance, cancellationToken);
    }

    private static AvatarAppearanceDto MapAppearance(UserAppearanceProjection projection)
        => new(
            new AvatarConfigDto(
                projection.SkinId,
                projection.HairId,
                projection.ClothingId,
                projection.AccessoryId,
                projection.EmojiId,
                projection.FrameId,
                projection.BackgroundId,
                projection.EffectId,
                projection.LeaderboardDecorationId,
                projection.AvatarVersion),
            projection.SkinAssetPath,
            projection.HairAssetPath,
            projection.ClothingAssetPath,
            projection.AccessoryAssetPath,
            projection.EmojiAssetPath,
            projection.FrameAssetPath,
            projection.BackgroundAssetPath,
            projection.EffectAssetPath,
            projection.LeaderboardDecorationAssetPath);
}
