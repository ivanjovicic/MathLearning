using MathLearning.Application.DTOs.Quiz;
using MathLearning.Application.Services;
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
            ApiDbContext db,
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
            ApiDbContext db,
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
            ApiDbContext db,
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

        // 📤 OFFLINE BATCH SUBMIT (Improved - Idempotent + Server Validation)
        group.MapPost("/offline-submit", async (
            OfflineBatchSubmitRequest request,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            if (request.Answers == null || !request.Answers.Any())
                return Results.BadRequest("No answers to import");

            // Proveri ili kreiraj quiz session
            Guid sessionId;
            if (!Guid.TryParse(request.SessionId, out sessionId))
            {
                sessionId = Guid.NewGuid();
            }

            var session = await db.QuizSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null)
            {
                session = new QuizSession
                {
                    Id = sessionId,
                    UserId = userId,
                    StartedAt = request.Answers.Min(a => a.AnsweredAt)
                };
                db.QuizSessions.Add(session);
            }

            int importedCount = 0;
            
            // Batch učitaj sva pitanja odjednom (optimizacija)
            var questionIds = request.Answers.Select(a => a.QuestionId).Distinct().ToList();
            var questions = await db.Questions
                .Include(q => q.Options)
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id);

            // Batch učitaj postojeće statistike
            var existingStats = await db.UserQuestionStats
                .Where(s => s.UserId == userId && questionIds.Contains(s.QuestionId))
                .ToDictionaryAsync(s => s.QuestionId);

            foreach (var answer in request.Answers)
            {
                // 🔒 IDEMPOTENCY CHECK - Proveri da li već postoji identičan answer
                bool exists = await db.UserAnswers
                    .AnyAsync(x =>
                        x.UserId == userId &&
                        x.QuestionId == answer.QuestionId &&
                        x.AnsweredAt == answer.AnsweredAt);

                if (exists)
                    continue;

                if (!questions.TryGetValue(answer.QuestionId, out var question))
                    continue;

                // 🔐 SERVER-SIDE VALIDATION - Ne veruj IsCorrectOffline od klijenta
                bool isCorrectServer = question.Type == "multiple_choice"
                    ? question.Options.Any(o => o.IsCorrect && o.Text == answer.Answer)
                    : question.CorrectAnswer != null && 
                      question.CorrectAnswer.Trim().Equals(answer.Answer.Trim(), 
                          StringComparison.OrdinalIgnoreCase);

                // Dodaj odgovor sa server-validiranom tačnošću
                db.UserAnswers.Add(new UserAnswer
                {
                    UserId = userId,
                    QuestionId = answer.QuestionId,
                    QuizSessionId = sessionId,
                    Answer = answer.Answer,
                    IsCorrect = isCorrectServer, // ⚠️ Koristimo server validaciju, ne klijentovu
                    TimeSpentSeconds = answer.TimeSpent,
                    AnsweredAt = answer.AnsweredAt
                });

                // Ažuriraj statistiku
                if (!existingStats.TryGetValue(answer.QuestionId, out var stat))
                {
                    stat = new UserQuestionStat
                    {
                        UserId = userId,
                        QuestionId = answer.QuestionId,
                        Attempts = 0,
                        CorrectAttempts = 0
                    };
                    db.UserQuestionStats.Add(stat);
                    existingStats[answer.QuestionId] = stat;
                }

                stat.Attempts++;
                if (isCorrectServer)
                    stat.CorrectAttempts++;

                if (stat.LastAttemptAt == null || answer.AnsweredAt > stat.LastAttemptAt)
                    stat.LastAttemptAt = answer.AnsweredAt;

                importedCount++;
            }

            await db.SaveChangesAsync();

            // Izračunaj fresh XP, Level i Streak
            var overview = await CalculateUserOverview(db, userId);

            return Results.Ok(new OfflineBatchSubmitResponse(
                importedCount,
                overview.Xp,
                overview.Level,
                overview.Streak
            ));
        });

        // 📚 SRS UPDATE
        group.MapPost("/srs/update", async (
            SrsUpdateDto dto,
            ISrsService srs,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var result = await srs.UpdateAsync(userId, dto);

            return Results.Ok(new
            {
                questionId = result.QuestionId,
                nextReview = result.NextReview,
                streak = result.SuccessStreak,
                ease = result.Ease
            });
        });

        // 📅 SRS DAILY
        group.MapGet("/srs/daily", async (
            ApiDbContext db,
            HttpContext ctx,
            int limit = 20) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var questions = await db.QuestionStats
                .Where(x => x.UserId == userId && x.NextReview <= DateTime.UtcNow)
                .OrderBy(x => x.Ease)
                .Take(limit)
                .Select(x => x.Question)
                .ToListAsync();

            return Results.Ok(questions);
        });

        // 🔀 SRS MIXED (due + random)
        group.MapGet("/srs/mixed", async (
            ApiDbContext db,
            HttpContext ctx,
            int count = 15) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var dueStats = await db.QuestionStats
                .Where(x => x.UserId == userId && x.NextReview <= DateTime.UtcNow)
                .OrderBy(x => x.Ease)
                .Take(count)
                .ToListAsync();

            var dueIds = dueStats.Select(x => x.QuestionId).ToList();

            var srsQuestions = await db.Questions
                .Include(q => q.Options)
                .Where(q => dueIds.Contains(q.Id))
                .ToListAsync();

            int needed = count - srsQuestions.Count;

            List<Question> randomQuestions = new();

            if (needed > 0)
            {
                randomQuestions = await db.Questions
                    .Include(q => q.Options)
                    .Where(x => !dueIds.Contains(x.Id))
                    .OrderBy(x => Guid.NewGuid())
                    .Take(needed)
                    .ToListAsync();
            }

            return Results.Ok(new
            {
                srs = srsQuestions,
                random = randomQuestions
            });
        });

        // 🔥 SRS STREAK
        group.MapGet("/srs/streak", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            return Results.Ok(new
            {
                streak = profile?.Streak ?? 0
            });
        });
    }

    // 📊 Helper za računanje XP, Level i Streak
    private static async Task<(int Xp, int Level, int Streak)> CalculateUserOverview(
        ApiDbContext db, 
        int userId)
    {
        // XP i Level
        var stats = await db.UserQuestionStats
            .Where(s => s.UserId == userId)
            .ToListAsync();

        int totalCorrect = stats.Sum(s => s.CorrectAttempts);
        int xp = totalCorrect * 10;
        int level = 1 + (xp / 100);

        // Streak
        var answerDays = await db.UserAnswers
            .Where(a => a.UserId == userId)
            .Select(a => a.AnsweredAt.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();

        int streak = 0;
        DateTime today = DateTime.UtcNow.Date;

        foreach (var day in answerDays)
        {
            if (day == today || day == today.AddDays(-streak))
                streak++;
            else
                break;
        }

        return (xp, level, streak);
    }
}
