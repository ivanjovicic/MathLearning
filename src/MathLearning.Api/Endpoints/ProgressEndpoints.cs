using System.Text.Json;
using MathLearning.Application.DTOs.Progress;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class ProgressEndpoints
{
    public static void MapProgressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/progress")
                       .RequireAuthorization()
                       .WithTags("Progress");

        group.MapGet("/overview", async (
            ApiDbContext db,
            ICosmeticRewardService cosmeticRewardService,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var aggregate = await db.UserQuestionStats
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalAttempts = g.Sum(x => x.Attempts),
                    TotalCorrect = g.Sum(x => x.CorrectAttempts)
                })
                .FirstOrDefaultAsync();

            int totalAttempts = aggregate?.TotalAttempts ?? 0;
            int totalCorrect = aggregate?.TotalCorrect ?? 0;

            double accuracy = totalAttempts == 0
                ? 0
                : Math.Round((double)totalCorrect / totalAttempts * 100, 2);

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            StreakRollEventDto? streakEvent = null;

            if (profile != null)
            {
                var roll = StreakRoller.Apply(profile, today);
                if (roll != null)
                {
                    streakEvent = new StreakRollEventDto(
                        roll.Type,
                        roll.MissedDays,
                        roll.FreezesUsed,
                        roll.StreakBefore,
                        roll.StreakAfter
                    );
                    await db.SaveChangesAsync();
                    await cosmeticRewardService.ProcessProgressRewardsAsync(userId, ctx.RequestAborted);
                }
            }

            int streak = profile?.Streak ?? 0;

            return Results.Ok(new ProgressOverviewDto(
                totalAttempts,
                accuracy,
                streak,
                StreakFreezeCount: profile?.StreakFreezeCount ?? 0,
                LastStreakDay: profile?.LastStreakDay,
                LastActivityDay: profile?.LastActivityDay,
                StreakEvent: streakEvent
            ));
        });

        group.MapGet("/weak-areas", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var weakAreas = await (
                from q in db.Questions.AsNoTracking()
                join s in db.UserQuestionStats.AsNoTracking()
                    .Where(x => x.UserId == userId)
                    on q.Id equals s.QuestionId
                join st in db.Subtopics.AsNoTracking()
                    on q.SubtopicId equals st.Id
                group new { s, st } by new { st.Id, st.Name } into g
                let attempts = g.Sum(x => x.s.Attempts)
                let correct = g.Sum(x => x.s.CorrectAttempts)
                where attempts >= 5
                select new WeakAreaDto(
                    g.Key.Id,
                    g.Key.Name,
                    Math.Round((double)correct / attempts * 100, 2)
                )
            )
            .OrderBy(x => x.Accuracy)
            .Take(5)
            .ToListAsync();

            return Results.Ok(weakAreas);
        });

        group.MapGet("/topics", async (
            ApiDbContext db,
            HttpContext ctx) =>
            await GetTopicProgressAsync(db, ctx));

        app.MapGet("/api/topics/progress", async (
            ApiDbContext db,
            HttpContext ctx) =>
            await GetTopicProgressAsync(db, ctx))
            .RequireAuthorization()
            .WithTags("Progress")
            .WithName("GetTopicProgressLegacyAlias");

        group.MapPost("/sync", async (
            JsonElement payload,
            ApiDbContext db,
            ICosmeticRewardService cosmeticRewardService,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            bool completed = false;
            var day = DateOnly.FromDateTime(DateTime.UtcNow);

            if (TryGetBool(payload, "completed", out var completedValue))
                completed = completedValue;
            if (TryGetString(payload, "day", out var dayText) && DateOnly.TryParse(dayText, out var parsedDay))
                day = parsedDay;

            var stat = await db.UserDailyStats
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Day == day);

            if (stat == null)
            {
                db.UserDailyStats.Add(new Domain.Entities.UserDailyStat
                {
                    UserId = userId,
                    Day = day,
                    Completed = completed
                });
            }
            else if (completed)
            {
                stat.Completed = true;
            }

            await db.SaveChangesAsync();
            await cosmeticRewardService.ProcessProgressRewardsAsync(userId, ctx.RequestAborted);

            return Results.Ok(new
            {
                success = true,
                syncedAt = DateTime.UtcNow
            });
        });
    }

    private static async Task<IResult> GetTopicProgressAsync(
        ApiDbContext db,
        HttpContext ctx)
    {
        string userId = ctx.User.FindFirst("userId")!.Value;

        var orderedTopics = await db.Topics
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        var topicAggregates = await (
            from s in db.UserQuestionStats.AsNoTracking()
                .Where(x => x.UserId == userId)
            join q in db.Questions.AsNoTracking()
                on s.QuestionId equals q.Id
            join sub in db.Subtopics.AsNoTracking()
                on q.SubtopicId equals sub.Id
            group s by sub.TopicId into g
            select new
            {
                TopicId = g.Key,
                Attempts = g.Sum(x => x.Attempts),
                Correct = g.Sum(x => x.CorrectAttempts)
            }
        ).ToListAsync();

        var aggregateByTopicId = topicAggregates
            .ToDictionary(
                x => x.TopicId,
                x => x.Attempts == 0 ? 0 : (x.Correct * 100.0 / x.Attempts));

        var result = new List<TopicProgressDto>();

        for (int i = 0; i < orderedTopics.Count; i++)
        {
            var topic = orderedTopics[i];
            var accuracy = aggregateByTopicId.TryGetValue(topic.Id, out var value)
                ? value
                : 0;

            bool unlocked;

            if (i == 0)
            {
                unlocked = true;
            }
            else
            {
                var previousTopic = orderedTopics[i - 1];
                double prevAccuracy = aggregateByTopicId.TryGetValue(previousTopic.Id, out var prevValue)
                    ? prevValue
                    : 0;

                unlocked = prevAccuracy >= 60.0;
            }

            result.Add(new TopicProgressDto(
                topic.Id,
                topic.Name,
                Math.Round(accuracy, 2),
                unlocked
            ));
        }

        return Results.Ok(result);
    }

    private static bool TryGetBool(JsonElement json, string property, out bool value)
    {
        value = false;
        if (!json.TryGetProperty(property, out var p))
            return false;

        if (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False)
        {
            value = p.GetBoolean();
            return true;
        }

        if (p.ValueKind == JsonValueKind.String && bool.TryParse(p.GetString(), out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool TryGetString(JsonElement json, string property, out string? value)
    {
        value = null;
        if (!json.TryGetProperty(property, out var p))
            return false;
        if (p.ValueKind != JsonValueKind.String)
            return false;

        value = p.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }
}
