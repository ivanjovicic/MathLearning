using MathLearning.Application.DTOs.Progress;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MathLearning.Api.Endpoints;

public static class ProgressEndpoints
{
    public static void MapProgressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/progress")
                       .RequireAuthorization()
                       .WithTags("Progress");

        // 📊 OVERVIEW
        group.MapGet("/overview", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var stats = await db.UserQuestionStats
                .Where(s => s.UserId == userId)
                .ToListAsync();

            int totalAttempts = stats.Sum(s => s.Attempts);
            int totalCorrect = stats.Sum(s => s.CorrectAttempts);

            double accuracy = totalAttempts == 0
                ? 0
                : Math.Round((double)totalCorrect / totalAttempts * 100, 2);

            int streak = await CalculateDailyStreak(db, userId);

            return Results.Ok(new ProgressOverviewDto(
                totalAttempts,
                accuracy,
                streak
            ));
        });

        // ⚠️ WEAK AREAS
        group.MapGet("/weak-areas", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var weakAreas = await (
                from q in db.Questions
                join s in db.UserQuestionStats
                    .Where(x => x.UserId == userId)
                    on q.Id equals s.QuestionId
                join st in db.Subtopics
                    on q.SubtopicId equals st.Id
                group new { s, st } by new { st.Id, st.Name } into g
                let attempts = g.Sum(x => x.s.Attempts)
                let correct = g.Sum(x => x.s.CorrectAttempts)
                where attempts >= 5 // filter šuma
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

        // 📚 TOPIC PROGRESS
        group.MapGet("/topics", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var topics = await (
                from q in db.Questions
                join s in db.UserQuestionStats.Where(x => x.UserId == userId)
                    on q.Id equals s.QuestionId into stats
                from stat in stats.DefaultIfEmpty()
                join sub in db.Subtopics on q.SubtopicId equals sub.Id
                join t in db.Topics on sub.TopicId equals t.Id
                group stat by new { t.Id, t.Name } into g
                select new
                {
                    TopicId = g.Key.Id,
                    Name = g.Key.Name,
                    Accuracy = g.Sum(x => x == null ? 0 : x.CorrectAttempts) * 100.0 /
                               Math.Max(g.Sum(x => x == null ? 0 : x.Attempts), 1)
                }
            ).OrderBy(x => x.TopicId).ToListAsync();

            var result = new List<TopicProgressDto>();

            for (int i = 0; i < topics.Count; i++)
            {
                bool unlocked;

                if (i == 0)
                {
                    // Prva lekcija je uvek otključana
                    unlocked = true;
                }
                else
                {
                    double prevAccuracy = topics[i - 1].Accuracy;

                    unlocked = prevAccuracy >= 60.0;
                }

                result.Add(new TopicProgressDto(
                    topics[i].TopicId,
                    topics[i].Name,
                    Math.Round(topics[i].Accuracy, 2),
                    unlocked
                ));
            }

            return Results.Ok(result);
        });

        // 🔄 SYNC (legacy mobile endpoint)
        group.MapPost("/sync", async (
            JsonElement payload,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

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

            return Results.Ok(new
            {
                success = true,
                syncedAt = DateTime.UtcNow
            });
        });
    }

    // 🔥 STREAK LOGIKA
    private static async Task<int> CalculateDailyStreak(
        ApiDbContext db,
        int userId)
    {
        var days = await db.UserAnswers
            .Where(a => a.UserId == userId)
            .Select(a => a.AnsweredAt.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();

        int streak = 0;
        DateTime today = DateTime.UtcNow.Date;

        foreach (var day in days)
        {
            if (day == today || day == today.AddDays(-streak))
                streak++;
            else
                break;
        }

        return streak;
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
