using MathLearning.Application.DTOs.Leaderboard;
using MathLearning.Application.DTOs.Progress;
using MathLearning.Application.Services;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Core.DTOs;
using MathLearning.Core.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Performance;
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
            var leaderboardUserIds = leaderboard.Select(x => x.UserId).Distinct().ToList();
            var leaderboardProfiles = await db.UserProfiles.AsNoTracking()
                .Where(p => leaderboardUserIds.Contains(p.UserId))
                .Select(p => new { p.UserId, Name = p.DisplayName ?? p.Username ?? ("User" + p.UserId), p.Level, p.Streak })
                .ToDictionaryAsync(p => p.UserId, ctx.RequestAborted);
            var appearanceMap = await LoadAppearanceMapAsync(db, leaderboardUserIds, ctx.RequestAborted);

            var items = leaderboard.Select(e =>
            {
                leaderboardProfiles.TryGetValue(e.UserId, out var up);
                return new LeaderboardItemDto
                {
                    Rank = e.Rank,
                    UserId = e.UserId,
                    DisplayName = up?.Name ?? e.DisplayName,
                    AvatarUrl = null,
                    Appearance = appearanceMap.TryGetValue(e.UserId, out var appearance) ? appearance : null,
                    Score = e.Xp,
                    StreakDays = up?.Streak ?? e.Streak,
                    Level = up?.Level ?? e.Level
                };
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
            [FromServices] ILogger<Program> logger,
            HttpContext ctx,
            string range = "allTime",
            int limit = 50) =>
        {
            var startedAt = DateTime.UtcNow;
            limit = Math.Clamp(limit, 1, 200);
            var rankedRows = await ToListAsync(
                CompiledQueries.GetGlobalLeaderboard(db, range, limit),
                ctx.RequestAborted);
            var appearanceMap = await LoadAppearanceMapAsync(db, rankedRows.Select(x => x.UserId), ctx.RequestAborted);

            var ranked = rankedRows
                .Select((x, index) => new
                {
                    rank = index + 1,
                    userId = x.UserId,
                    displayName = x.DisplayName ?? x.Username ?? $"User{x.UserId}",
                    level = x.Level,
                    xp = x.Xp,
                    weeklyXp = x.WeeklyXp,
                    streak = x.Streak,
                    appearance = appearanceMap.TryGetValue(x.UserId, out var appearance) ? appearance : null
                })
                .ToList();

            logger.LogInformation(
                "Global leaderboard query executed. Range={Range} Limit={Limit} Rows={RowCount} ElapsedMs={ElapsedMs}",
                range,
                limit,
                ranked.Count,
                Math.Round((DateTime.UtcNow - startedAt).TotalMilliseconds, 2));

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
            var friendsUserIds = leaderboard.Select(x => x.UserId).Distinct().ToList();
            var friendsProfiles = await db.UserProfiles.AsNoTracking()
                .Where(p => friendsUserIds.Contains(p.UserId))
                .Select(p => new { p.UserId, Name = p.DisplayName ?? p.Username ?? ("User" + p.UserId), p.Level, p.Streak })
                .ToDictionaryAsync(p => p.UserId, ctx.RequestAborted);
            var appearanceMap = await LoadAppearanceMapAsync(db, friendsUserIds, ctx.RequestAborted);

            var items = leaderboard.Select(e =>
            {
                friendsProfiles.TryGetValue(e.UserId, out var up);
                return new LeaderboardItemDto
                {
                    Rank = e.Rank,
                    UserId = e.UserId,
                    DisplayName = up?.Name ?? e.DisplayName,
                    AvatarUrl = null,
                    Appearance = appearanceMap.TryGetValue(e.UserId, out var appearance) ? appearance : null,
                    Score = e.Xp,
                    StreakDays = up?.Streak ?? e.Streak,
                    Level = up?.Level ?? e.Level
                };
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

        group.MapGet("/student", async (
            [FromServices] IStudentLeaderboardService studentLeaderboardService,
            HttpContext ctx,
            string scope = "global",
            string period = "all_time",
            int limit = 50,
            string? cursor = null,
            bool includeMe = true,
            CancellationToken ct = default) =>
        {
            var userId = ctx.User.FindFirst("userId")!.Value;
            var result = await studentLeaderboardService.GetLeaderboardAsync(userId, scope, period, limit, cursor, includeMe, ct);
            return Results.Ok(result);
        })
        .WithName("GetStudentLeaderboard")
        .WithSummary("Get student leaderboard (DB-backed, full cosmetics and badge processing)");

        group.MapPost("/admin/add-xp/{userId}", async (
            [FromServices] IXpTrackingService xpService,
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
            [FromServices] IXpTrackingService xpService,
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

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source, CancellationToken cancellationToken)
    {
        var results = new List<T>();
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            results.Add(item);
        }

        return results;
    }
}
