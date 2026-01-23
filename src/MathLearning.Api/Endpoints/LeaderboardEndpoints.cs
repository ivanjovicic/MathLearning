using MathLearning.Application.DTOs.Progress;
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

            // GLOBAL: all-time ili weekly
            group.MapGet("/global", async (
                ApiDbContext db,
                HttpContext ctx,
                string range = "allTime",   // allTime | weekly
                int limit = 50
            ) =>
            {
                int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

                // 1️⃣ GLOBAL XP iz UserQuestionStats (bez potrebe za User tabelom)
                var baseQuery = from s in db.UserQuestionStats
                                group s by s.UserId into g
                                select new
                                {
                                    UserId = g.Key,
                                    TotalCorrect = g.Sum(x => x.CorrectAttempts)
                                };

                var globalList = await baseQuery.ToListAsync();

                // XP i Level
                var globalWithXp = globalList
                    .Select(x =>
                    {
                        int xp = x.TotalCorrect * 10;
                        int level = 1 + xp / 100;
                        return new
                        {
                            x.UserId,
                            DisplayName = $"User{x.UserId}", // Placeholder dok ne integrišeš sa Admin Identity
                            Xp = xp,
                            Level = level
                        };
                    })
                    .OrderByDescending(x => x.Xp)
                    .ToList();

                // 2️⃣ WEEKLY XP (iz UserAnswers – poslednjih 7 dana)
                DateTime weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.Date.DayOfWeek + 1);
                // ponedeljak kao start (DayOfWeek Monday = 1)

                var weeklyRaw = await db.UserAnswers
                    .Where(a => a.AnsweredAt >= weekStart && a.IsCorrect)
                    .GroupBy(a => a.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        WeeklyCorrect = g.Count()
                    })
                    .ToListAsync();

                var weeklyDict = weeklyRaw.ToDictionary(
                    x => x.UserId,
                    x => x.WeeklyCorrect * 10 // weekly XP
                );

                // 3️⃣ Streak – možemo reuse iz ProgressEndpoints (kopiraj helper)
                var streakDict = await CalculateStreaksForUsers(db);

                // 4️⃣ Sklopi final listu
                var enriched = globalWithXp
                    .Select(x =>
                    {
                        weeklyDict.TryGetValue(x.UserId, out var wXp);
                        streakDict.TryGetValue(x.UserId, out var streak);
                        return new
                        {
                            x.UserId,
                            x.DisplayName,
                            x.Xp,
                            x.Level,
                            WeeklyXp = wXp,
                            Streak = streak
                        };
                    })
                    .ToList();

                // 5️⃣ Odaberi sort u zavisnosti od range
                IEnumerable<dynamic> ordered = range.ToLower() switch
                {
                    "weekly" => enriched.OrderByDescending(x => x.WeeklyXp),
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
            });

            // FRIENDS leaderboard
            group.MapGet("/friends", async (
                ApiDbContext db,
                HttpContext ctx,
                string range = "weekly", // weekly | allTime
                int limit = 50
            ) =>
            {
                int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

                // Skup friend + user sam
                var friendIds = await db.UserFriends
                    .Where(f => f.UserId == userId)
                    .Select(f => f.FriendId)
                    .ToListAsync();

                friendIds.Add(userId);

                if (!friendIds.Any())
                {
                    return Results.Ok(Array.Empty<LeaderboardEntryDto>());
                }

                // 1️⃣ Global XP za prijatelje
                var baseQuery = from s in db.UserQuestionStats
                                where friendIds.Contains(s.UserId)
                                group s by s.UserId into g
                                select new
                                {
                                    UserId = g.Key,
                                    TotalCorrect = g.Sum(x => x.CorrectAttempts)
                                };

                var globalList = await baseQuery.ToListAsync();

                var globalWithXp = globalList
                    .Select(x =>
                    {
                        int xp = x.TotalCorrect * 10;
                        int level = 1 + xp / 100;
                        return new
                        {
                            x.UserId,
                            DisplayName = $"User{x.UserId}",
                            Xp = xp,
                            Level = level
                        };
                    })
                    .ToList();

                // 2️⃣ Weekly XP
                DateTime weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.Date.DayOfWeek + 1);

                var weeklyRaw = await db.UserAnswers
                    .Where(a => a.AnsweredAt >= weekStart && friendIds.Contains(a.UserId) && a.IsCorrect)
                    .GroupBy(a => a.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        WeeklyCorrect = g.Count()
                    })
                    .ToListAsync();

                var weeklyDict = weeklyRaw.ToDictionary(
                    x => x.UserId,
                    x => x.WeeklyCorrect * 10
                );

                // 3️⃣ Streak
                var streakDict = await CalculateStreaksForUsers(db);

                // 4️⃣ Sklopi final listu
                var enriched = globalWithXp
                    .Select(x =>
                    {
                        weeklyDict.TryGetValue(x.UserId, out var wXp);
                        streakDict.TryGetValue(x.UserId, out var streak);
                        return new
                        {
                            x.UserId,
                            x.DisplayName,
                            x.Xp,
                            x.Level,
                            WeeklyXp = wXp,
                            Streak = streak
                        };
                    })
                    .ToList();

                // 5️⃣ Sort po range-u
                IEnumerable<dynamic> ordered = range.ToLower() switch
                {
                    "alltime" => enriched.OrderByDescending(x => x.Xp),
                    _ => enriched.OrderByDescending(x => x.WeeklyXp)
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
            });
        }

        // 🔥 helper – streak za SVE korisnike (optimizovana verzija)
        private static async Task<Dictionary<int, int>> CalculateStreaksForUsers(ApiDbContext db)
        {
            // sve dane po useru
            var data = await db.UserAnswers
                .Select(a => new { a.UserId, Day = a.AnsweredAt.Date })
                .Distinct()
                .ToListAsync();

            var grouped = data
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Day).OrderByDescending(d => d).ToList());

            var result = new Dictionary<int, int>();
            DateTime today = DateTime.UtcNow.Date;

            foreach (var kvp in grouped)
            {
                int userId = kvp.Key;
                var days = kvp.Value;
                int streak = 0;

                foreach (var day in days)
                {
                    if (day == today || day == today.AddDays(-streak))
                        streak++;
                    else
                        break;
                }

                result[userId] = streak;
            }

            return result;
        }
    }

}
