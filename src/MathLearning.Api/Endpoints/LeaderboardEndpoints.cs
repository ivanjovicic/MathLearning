using MathLearning.Application.DTOs.Progress;
using MathLearning.Application.DTOs.Leaderboard;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints
{
    public static class LeaderboardEndpoints
    {
        public static void MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/leaderboard")
                           .RequireAuthorization()
                           .WithTags("Leaderboard");

            // 🏆 Enhanced leaderboard with cursor-based pagination
            group.MapGet("", async (
                LeaderboardService leaderboardService,
                HttpContext ctx,
                string scope = "global",     // global | school | faculty | friends
                string period = "all_time",  // all_time | week | month | day
                int limit = 50,
                string? cursor = null,       // Base64 cursor for pagination
                bool includeMe = true        // Include current user's position
            ) =>
            {
                string userId = ctx.User.FindFirst("userId")!.Value;
                var result = await leaderboardService.GetLeaderboardAsync(
                    userId, scope, period, limit, cursor, includeMe);
                return Results.Ok(result);
            })
            .WithName("GetLeaderboard")
            .WithSummary("Get leaderboard with cursor-based pagination, badges, and percentile");

            // 🏫 School vs School aggregate leaderboard
            group.MapGet("/schools", async (
                LeaderboardService leaderboardService,
                HttpContext ctx,
                string period = "week",   // all_time | week | month | day
                int limit = 50,
                string? cursor = null
            ) =>
            {
                string userId = ctx.User.FindFirst("userId")!.Value;
                var result = await leaderboardService.GetSchoolLeaderboardAsync(userId, period, limit, cursor);
                return Results.Ok(result);
            })
            .WithName("GetSchoolLeaderboard")
            .WithSummary("Get school vs school aggregate leaderboard");

            // LEGACY: GLOBAL leaderboard (all-time or weekly) - kept for backward compatibility
            group.MapGet("/global", async (
                ApiDbContext db,
                HttpContext ctx,
                string range = "allTime",   // allTime | weekly
                int limit = 50
            ) =>
            {
                string userId = ctx.User.FindFirst("userId")!.Value;

                // Profile lookup for real display names
                var profileDict = (await db.UserProfiles
                    .Select(p => new { p.UserId, p.DisplayName, p.Username, p.Level, p.Streak })
                    .ToListAsync())
                    .ToDictionary(p => p.UserId);

                // Global XP
                var globalList = await (
                    from s in db.UserQuestionStats
                    group s by s.UserId into g
                    select new
                    {
                        UserId = g.Key,
                        TotalCorrect = g.Sum(x => x.CorrectAttempts)
                    }
                ).ToListAsync();

                // Weekly XP
                DateTime weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.Date.DayOfWeek + 1);

                var weeklyDict = (await db.UserAnswers
                    .Where(a => a.AnsweredAt >= weekStart && a.IsCorrect)
                    .GroupBy(a => a.UserId)
                    .Select(g => new { UserId = g.Key, WeeklyCorrect = g.Count() })
                    .ToListAsync())
                    .ToDictionary(x => x.UserId, x => x.WeeklyCorrect * 10);

                // Build entries
                var enriched = globalList.Select(x =>
                {
                    int xp = x.TotalCorrect * 10;
                    profileDict.TryGetValue(x.UserId, out var profile);
                    weeklyDict.TryGetValue(x.UserId, out var wXp);

                    return new
                    {
                        x.UserId,
                        DisplayName = profile?.DisplayName ?? profile?.Username ?? $"User{x.UserId}",
                        Level = profile?.Level ?? (1 + xp / 100),
                        Xp = xp,
                        WeeklyXp = wXp,
                        Streak = profile?.Streak ?? 0
                    };
                }).ToList();

                // Sort by range
                var ordered = range.ToLower() switch
                {
                    "weekly" => enriched.OrderByDescending(x => x.WeeklyXp).ThenByDescending(x => x.Xp),
                    _ => enriched.OrderByDescending(x => x.Xp)
                };

                var ranked = ordered
                    .Take(limit)
                    .Select((x, index) => new LeaderboardEntryDto(
                        Rank: index + 1,
                        UserId: x.UserId,
                        DisplayName: x.DisplayName,
                        Level: x.Level,
                        Xp: x.Xp,
                        WeeklyXp: x.WeeklyXp,
                        Streak: x.Streak
                    ))
                    .ToList();

                return Results.Ok(ranked);
            })
            .WithName("GetGlobalLeaderboard")
            .WithSummary("Get global leaderboard (legacy endpoint)");

            // LEGACY: FRIENDS leaderboard - kept for backward compatibility
            group.MapGet("/friends", async (
                ApiDbContext db,
                HttpContext ctx,
                string range = "weekly",
                int limit = 50
            ) =>
            {
                string userId = ctx.User.FindFirst("userId")!.Value;

                var friendIds = await db.UserFriends
                    .Where(f => f.UserId == userId)
                    .Select(f => f.FriendId)
                    .ToListAsync();

                friendIds.Add(userId);

                if (!friendIds.Any())
                    return Results.Ok(Array.Empty<LeaderboardEntryDto>());

                // Profile lookup
                var profileDict = (await db.UserProfiles
                    .Where(p => friendIds.Contains(p.UserId))
                    .Select(p => new { p.UserId, p.DisplayName, p.Username, p.Level, p.Streak })
                    .ToListAsync())
                    .ToDictionary(p => p.UserId);

                // Global XP
                var globalList = await (
                    from s in db.UserQuestionStats
                    where friendIds.Contains(s.UserId)
                    group s by s.UserId into g
                    select new
                    {
                        UserId = g.Key,
                        TotalCorrect = g.Sum(x => x.CorrectAttempts)
                    }
                ).ToListAsync();

                // Weekly XP
                DateTime weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.Date.DayOfWeek + 1);

                var weeklyDict = (await db.UserAnswers
                    .Where(a => a.AnsweredAt >= weekStart && friendIds.Contains(a.UserId) && a.IsCorrect)
                    .GroupBy(a => a.UserId)
                    .Select(g => new { UserId = g.Key, WeeklyCorrect = g.Count() })
                    .ToListAsync())
                    .ToDictionary(x => x.UserId, x => x.WeeklyCorrect * 10);

                // Build entries
                var enriched = globalList.Select(x =>
                {
                    int xp = x.TotalCorrect * 10;
                    profileDict.TryGetValue(x.UserId, out var profile);
                    weeklyDict.TryGetValue(x.UserId, out var wXp);

                    return new
                    {
                        x.UserId,
                        DisplayName = profile?.DisplayName ?? profile?.Username ?? $"User{x.UserId}",
                        Level = profile?.Level ?? (1 + xp / 100),
                        Xp = xp,
                        WeeklyXp = wXp,
                        Streak = profile?.Streak ?? 0
                    };
                }).ToList();

                // Sort by range
                var ordered = range.ToLower() switch
                {
                    "alltime" => enriched.OrderByDescending(x => x.Xp),
                    _ => enriched.OrderByDescending(x => x.WeeklyXp).ThenByDescending(x => x.Xp)
                };

                var ranked = ordered
                    .Take(limit)
                    .Select((x, index) => new LeaderboardEntryDto(
                        Rank: index + 1,
                        UserId: x.UserId,
                        DisplayName: x.DisplayName,
                        Level: x.Level,
                        Xp: x.Xp,
                        WeeklyXp: x.WeeklyXp,
                        Streak: x.Streak
                    ))
                    .ToList();

                return Results.Ok(ranked);
            })
            .WithName("GetFriendsLeaderboard")
            .WithSummary("Get friends leaderboard (legacy endpoint)");
        }
    }
}
