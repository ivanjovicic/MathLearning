using MathLearning.Application.DTOs.Quiz;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class QuizEndpoints
{
    public static void MapQuizEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quiz")
                       .RequireAuthorization()
                       .WithTags("Quiz");

        // 🎬 START QUIZ
        group.MapPost("/start", async (
            StartQuizRequest request,
            AppDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var quiz = new QuizSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StartedAt = DateTime.UtcNow
            };

            db.QuizSessions.Add(quiz);

            var questions = await db.Questions
                .Where(q => q.SubtopicId == request.SubtopicId)
                .OrderBy(q => Guid.NewGuid())
                .Take(request.QuestionCount)
                .Select(q => new QuestionDto(
                    q.Id,
                    q.Type,
                    q.Text,
                    q.Options.Select(o => new OptionDto(o.Id, o.Text)).ToList()
                ))
                .ToListAsync();

            await db.SaveChangesAsync();

            return Results.Ok(new QuizResponse(quiz.Id, questions));
        });

        // 📝 NEXT QUESTION (adaptive learning)
        group.MapPost("/next-question", async (
            NextQuestionRequest request,
            AppDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var question = await (
                from q in db.Questions
                join s in db.UserQuestionStats
                    .Where(x => x.UserId == userId)
                    on q.Id equals s.QuestionId into stats
                from stat in stats.DefaultIfEmpty()
                where q.SubtopicId == request.SubtopicId
                orderby
                    // 1️⃣ pitanja koja korisnik najviše greši
                    (stat == null ? 1 : 
                     (double)stat.CorrectAttempts / Math.Max(stat.Attempts, 1)) ascending,

                    // 2️⃣ lakša pitanja prva
                    q.Difficulty ascending,

                    // 3️⃣ pitanja koja dugo nije video
                    stat.LastAttemptAt ascending
                select new
                {
                    Question = q
                }
            ).FirstOrDefaultAsync();

            if (question == null)
                return Results.NotFound("No questions available");

            var qEntity = question.Question;

            return Results.Ok(new NextQuestionResponse(
                qEntity.Id,
                qEntity.Type,
                qEntity.Text,
                qEntity.Options
                    .Select(o => new OptionDto(o.Id, o.Text))
                    .ToList()
            ));
        });

        // ✍️ SUBMIT ANSWER
        group.MapPost("/answer", async (
            SubmitAnswerRequest request,
            AppDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var question = await db.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == request.QuestionId);

            if (question == null)
                return Results.NotFound("Question not found");

            bool isCorrect =
                question.Type == "multiple_choice"
                    ? question.Options.Any(o =>
                        o.IsCorrect && o.Text == request.Answer)
                    : question.CorrectAnswer != null && question.CorrectAnswer.Trim()
                        .Equals(request.Answer.Trim(),
                            StringComparison.OrdinalIgnoreCase);

            db.UserAnswers.Add(new UserAnswer
            {
                UserId = userId,
                QuestionId = question.Id,
                QuizSessionId = request.QuizId,
                Answer = request.Answer,
                IsCorrect = isCorrect,
                TimeSpentSeconds = request.TimeSpentSeconds,
                AnsweredAt = DateTime.UtcNow
            });

            // Update user question statistics
            var stat = await db.UserQuestionStats
                .FirstOrDefaultAsync(s =>
                    s.UserId == userId &&
                    s.QuestionId == question.Id);

            if (stat == null)
            {
                stat = new UserQuestionStat
                {
                    UserId = userId,
                    QuestionId = question.Id,
                    Attempts = 0,
                    CorrectAttempts = 0
                };
                db.UserQuestionStats.Add(stat);
            }

            stat.Attempts++;
            if (isCorrect)
                stat.CorrectAttempts++;

            stat.LastAttemptAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new SubmitAnswerResponse(
                isCorrect,
                isCorrect ? null : question.Explanation
            ));
        });
    }
}
