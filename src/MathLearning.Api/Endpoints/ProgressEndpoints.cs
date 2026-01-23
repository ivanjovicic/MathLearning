using MathLearning.Application.DTOs.Progress;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

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
                join s in db.UserQuestionStats
                    .Where(x => x.UserId == userId)
                    on q.Id equals s.QuestionId
                join sub in db.Subtopics
                    on q.SubtopicId equals sub.Id
                join t in db.Topics
                    on sub.TopicId equals t.Id
                group s by new { t.Id, t.Name } into g
                select new TopicProgressDto(
                    g.Key.Id,
                    g.Key.Name,
                    Math.Round(
                        (double)g.Sum(x => x.CorrectAttempts) /
                        Math.Max(g.Sum(x => x.Attempts), 1) * 100,
                        2
                    )
                )
            ).ToListAsync();

            return Results.Ok(topics);
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
}
