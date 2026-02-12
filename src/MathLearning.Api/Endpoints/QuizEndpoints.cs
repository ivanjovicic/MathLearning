using MathLearning.Application.DTOs.Quiz;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
            string lang = await ResolveUserLang(db, ctx, userId);

            var quiz = new QuizSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StartedAt = DateTime.UtcNow
            };

            db.QuizSessions.Add(quiz);

            var questionIds = await db.Questions
                .Where(q => q.SubtopicId == request.SubtopicId)
                .OrderBy(q => Guid.NewGuid())
                .Take(request.QuestionCount)
                .Select(q => q.Id)
                .ToListAsync();
            var questions = await LoadQuestionsWithDetailsByIds(db, questionIds);

            var questionDtos = questions.Select(q => MapQuestionDto(q, lang)).ToList();

            await db.SaveChangesAsync();

            return Results.Ok(new QuizResponse(quiz.Id, questionDtos));
        });

        // 📦 LEGACY MOBILE QUESTIONS ENDPOINT
        group.MapGet("/questions", async (
            ApiDbContext db,
            HttpContext ctx,
            string? topic,
            int? subtopicId,
            int count = 10) =>
        {
            var topicId = ParseTopicIdFromTopic(topic ?? string.Empty);
            return await BuildLegacyQuestionsResponse(
                db,
                ctx,
                count,
                subtopicId is > 0 ? subtopicId : null,
                topicId);
        });

        group.MapPost("/questions", async (
            JsonElement payload,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int count = 10;
            if (TryGetInt(payload, "count", out var countValue) && countValue > 0)
                count = countValue;

            int? subtopicId = null;
            int? topicId = null;
            if (TryGetInt(payload, "subtopicId", out var parsedSubtopic) && parsedSubtopic > 0)
            {
                subtopicId = parsedSubtopic;
            }
            else if (TryGetString(payload, "topic", out var topicRaw))
            {
                topicId = ParseTopicIdFromTopic(topicRaw!);
            }

            return await BuildLegacyQuestionsResponse(db, ctx, count, subtopicId, topicId);
        });

        // 📝 NEXT QUESTION (adaptive learning)
        group.MapPost("/next-question", async (
            NextQuestionRequest request,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);
            string lang = await ResolveUserLang(db, ctx, userId);

            var question = await (
                from q in db.Questions
                    .Include(q => q.Options).ThenInclude(o => o.Translations)
                    .Include(q => q.Translations)
                    .Include(q => q.Steps).ThenInclude(s => s.Translations)
                join s in db.UserQuestionStats
                    .Where(x => x.UserId == userId)
                    on q.Id equals s.QuestionId into stats
                from stat in stats.DefaultIfEmpty()
                where q.SubtopicId == request.SubtopicId
                orderby
                    (stat == null ? 1 : 
                     (double)stat.CorrectAttempts / Math.Max(stat.Attempts, 1)) ascending,
                    q.Difficulty ascending,
                    stat.LastAttemptAt ascending
                select q
            ).FirstOrDefaultAsync();

            if (question == null)
                return Results.NotFound("No questions available");

            var steps = StepEngine.GetSteps(question, lang);

            return Results.Ok(new NextQuestionResponse(
                question.Id,
                question.Type,
                TranslationHelper.GetText(question, lang),
                question.Options
                    .Select(o => new OptionDto(o.Id, TranslationHelper.GetOptionText(o, lang)))
                    .ToList(),
                question.Difficulty,
                TranslationHelper.GetHintLight(question, lang),
                TranslationHelper.GetHintMedium(question, lang),
                TranslationHelper.GetHintFull(question, lang),
                TranslationHelper.GetExplanation(question, lang),
                steps
            ));
        });

        // ✍️ SUBMIT ANSWER
        group.MapPost("/answer", async (
            JsonElement request,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);
            string lang = await ResolveUserLang(db, ctx, userId);

            if (!TryGetInt(request, "questionId", out var questionId) || questionId <= 0)
                return Results.BadRequest("QuestionId is required");
            if (!TryGetAnswer(request, out var answerText) || string.IsNullOrWhiteSpace(answerText))
                return Results.BadRequest("Answer is required");
            if (!TryGetInt(request, "timeSpentSeconds", out var timeSpentSeconds))
                timeSpentSeconds = 0;

            var question = await db.Questions
                .Include(q => q.Options)
                .Include(q => q.Translations)
                .Include(q => q.Steps).ThenInclude(s => s.Translations)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                return Results.NotFound("Question not found");

            bool isCorrect =
                question.Type == "multiple_choice"
                    ? question.Options.Any(o =>
                        o.IsCorrect && (
                            o.Text == answerText ||
                            (int.TryParse(answerText, out var selectedOptionId) && o.Id == selectedOptionId)))
                    : question.CorrectAnswer != null && question.CorrectAnswer.Trim()
                        .Equals(answerText.Trim(),
                            StringComparison.OrdinalIgnoreCase);

            Guid quizSessionId;
            string? quizIdRaw = null;
            if (TryGetString(request, "quizId", out var legacyQuizId))
                quizIdRaw = legacyQuizId;
            else if (TryGetString(request, "sessionId", out var sessionId))
                quizIdRaw = sessionId;

            if (!Guid.TryParse(quizIdRaw, out quizSessionId))
            {
                quizSessionId = Guid.NewGuid();
                db.QuizSessions.Add(new QuizSession
                {
                    Id = quizSessionId,
                    UserId = userId,
                    StartedAt = DateTime.UtcNow
                });
            }

            db.UserAnswers.Add(new UserAnswer
            {
                UserId = userId,
                QuestionId = question.Id,
                QuizSessionId = quizSessionId,
                Answer = answerText,
                IsCorrect = isCorrect,
                TimeSpentSeconds = timeSpentSeconds,
                AnsweredAt = DateTime.UtcNow
            });

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

            // Return steps only on incorrect answer
            var steps = isCorrect ? null : StepEngine.GetSteps(question, lang);

            return Results.Ok(new SubmitAnswerResponse(
                isCorrect,
                isCorrect ? null : TranslationHelper.GetExplanation(question, lang),
                steps
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
                    ? question.Options.Any(o => o.IsCorrect && (
                        o.Text == answer.Answer ||
                        (int.TryParse(answer.Answer, out var selectedOptionId) && o.Id == selectedOptionId)))
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

        // 📤 Legacy alias used by mobile app
        group.MapPost("/batch-submit", async (
            JsonElement payload,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);
            var sessionGuid = Guid.NewGuid();
            var answers = new List<OfflineAnswerDto>();

            if (payload.TryGetProperty("answers", out var answersNode) &&
                answersNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in answersNode.EnumerateArray())
                {
                    if (!TryGetInt(item, "questionId", out var questionId) || questionId <= 0)
                        continue;
                    if (!TryGetAnswer(item, out var answerText) || string.IsNullOrWhiteSpace(answerText))
                        continue;

                    if (!TryGetInt(item, "timeSpent", out var timeSpent))
                        TryGetInt(item, "timeSpentSeconds", out timeSpent);
                    TryGetBool(item, "isCorrectOffline", out var isCorrectOffline);

                    DateTime answeredAt = DateTime.UtcNow;
                    if (TryGetString(item, "answeredAt", out var answeredAtText) &&
                        DateTime.TryParse(answeredAtText, out var parsedAnsweredAt))
                    {
                        answeredAt = parsedAnsweredAt;
                    }

                    answers.Add(new OfflineAnswerDto(
                        questionId,
                        answerText!,
                        isCorrectOffline,
                        timeSpent,
                        answeredAt));
                }
            }

            if (answers.Count == 0)
                return Results.BadRequest("No answers to import");

            var session = new QuizSession
            {
                Id = sessionGuid,
                UserId = userId,
                StartedAt = answers.Min(a => a.AnsweredAt)
            };
            db.QuizSessions.Add(session);

            int importedCount = 0;
            var questionIds = answers.Select(a => a.QuestionId).Distinct().ToList();
            var questions = await db.Questions
                .Include(q => q.Options)
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id);
            var existingStats = await db.UserQuestionStats
                .Where(s => s.UserId == userId && questionIds.Contains(s.QuestionId))
                .ToDictionaryAsync(s => s.QuestionId);

            foreach (var answer in answers)
            {
                bool exists = await db.UserAnswers.AnyAsync(x =>
                    x.UserId == userId &&
                    x.QuestionId == answer.QuestionId &&
                    x.AnsweredAt == answer.AnsweredAt);
                if (exists)
                    continue;

                if (!questions.TryGetValue(answer.QuestionId, out var questionForAnswer))
                    continue;

                bool isCorrectServer = questionForAnswer.Type == "multiple_choice"
                    ? questionForAnswer.Options.Any(o => o.IsCorrect && (
                        o.Text == answer.Answer ||
                        (int.TryParse(answer.Answer, out var selectedOptionId) && o.Id == selectedOptionId)))
                    : questionForAnswer.CorrectAnswer != null &&
                      questionForAnswer.CorrectAnswer.Trim().Equals(answer.Answer.Trim(), StringComparison.OrdinalIgnoreCase);

                db.UserAnswers.Add(new UserAnswer
                {
                    UserId = userId,
                    QuestionId = answer.QuestionId,
                    QuizSessionId = sessionGuid,
                    Answer = answer.Answer,
                    IsCorrect = isCorrectServer,
                    TimeSpentSeconds = answer.TimeSpent,
                    AnsweredAt = answer.AnsweredAt
                });

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
            string lang = await ResolveUserLang(db, ctx, userId);

            var dueQuestionIds = await db.QuestionStats
                .Where(x => x.UserId == userId && x.NextReview <= DateTime.UtcNow)
                .OrderBy(x => x.Ease)
                .ThenBy(x => x.QuestionId)
                .Take(limit)
                .Select(x => x.QuestionId)
                .ToListAsync();
            var questions = await LoadQuestionsWithDetailsByIds(db, dueQuestionIds);

            // Legacy/mobile UX fallback:
            // if user has at least one due SRS item, pad the session with random questions
            // so quiz flow doesn't stop after a single card.
            int targetCount = Math.Min(limit, 10);
            if (questions.Count > 0 && questions.Count < targetCount)
            {
                var dueIds = questions.Select(q => q.Id).ToList();
                int needed = targetCount - questions.Count;

                var randomFillIds = await db.Questions
                    .Where(q => !dueIds.Contains(q.Id))
                    .OrderBy(q => Guid.NewGuid())
                    .Take(needed)
                    .Select(q => q.Id)
                    .ToListAsync();
                var randomFill = await LoadQuestionsWithDetailsByIds(db, randomFillIds);

                questions.AddRange(randomFill);
            }

            return Results.Ok(questions.Select(q => MapQuestionDto(q, lang)).ToList());
        });

        // 🔀 SRS MIXED (due + random)
        group.MapGet("/srs/mixed", async (
            ApiDbContext db,
            HttpContext ctx,
            int count = 15) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);
            string lang = await ResolveUserLang(db, ctx, userId);

            var dueStats = await db.QuestionStats
                .Where(x => x.UserId == userId && x.NextReview <= DateTime.UtcNow)
                .OrderBy(x => x.Ease)
                .Take(count)
                .ToListAsync();

            var dueIds = dueStats.Select(x => x.QuestionId).ToList();

            var srsQuestions = await LoadQuestionsWithDetailsByIds(db, dueIds);

            int needed = count - srsQuestions.Count;

            List<Question> randomQuestions = new();

            if (needed > 0)
            {
                var randomQuestionIds = await db.Questions
                    .Where(x => !dueIds.Contains(x.Id))
                    .OrderBy(x => Guid.NewGuid())
                    .Take(needed)
                    .Select(x => x.Id)
                    .ToListAsync();
                randomQuestions = await LoadQuestionsWithDetailsByIds(db, randomQuestionIds);
            }

            return Results.Ok(new
            {
                srs = srsQuestions.Select(q => MapQuestionDto(q, lang)),
                random = randomQuestions.Select(q => MapQuestionDto(q, lang))
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

    // 🗺️ Shared helper to map Question entity → QuestionDto with translation + steps
    private static QuestionDto MapQuestionDto(Question q, string lang)
    {
        return new QuestionDto(
            q.Id,
            q.Type,
            TranslationHelper.GetText(q, lang),
            q.Options.Select(o => new OptionDto(o.Id, TranslationHelper.GetOptionText(o, lang))).ToList(),
            q.Options.FirstOrDefault(o => o.IsCorrect)?.Id ?? 0,
            q.Difficulty,
            TranslationHelper.GetHintLight(q, lang),
            TranslationHelper.GetHintMedium(q, lang),
            TranslationHelper.GetHintFull(q, lang),
            TranslationHelper.GetExplanation(q, lang),
            StepEngine.GetSteps(q, lang)
        );
    }

    private static async Task<IResult> BuildLegacyQuestionsResponse(
        ApiDbContext db,
        HttpContext ctx,
        int count,
        int? subtopicId,
        int? topicId)
    {
        int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);
        string lang = await ResolveUserLang(db, ctx, userId);

        IQueryable<Question> query = db.Questions.AsQueryable();

        if (subtopicId.HasValue)
        {
            query = query.Where(q => q.SubtopicId == subtopicId.Value);
        }
        else if (topicId.HasValue)
        {
            query = query.Where(q => q.Subtopic != null && q.Subtopic.TopicId == topicId.Value);
        }

        var questionIds = await query
            .OrderBy(q => Guid.NewGuid())
            .Take(Math.Max(1, count))
            .Select(q => q.Id)
            .ToListAsync();
        var questions = await LoadQuestionsWithDetailsByIds(db, questionIds);

        var quizSession = new QuizSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartedAt = DateTime.UtcNow
        };
        db.QuizSessions.Add(quizSession);
        await db.SaveChangesAsync();

        var mapped = questions.Select(q => new
        {
            id = q.Id,
            text = TranslationHelper.GetText(q, lang),
            type = q.Type,
            options = q.Options
                .Select(o => new { id = o.Id, text = TranslationHelper.GetOptionText(o, lang) })
                .ToList(),
            correctAnswerId = q.Options.FirstOrDefault(o => o.IsCorrect)?.Id ?? 0,
            subtopicId = q.SubtopicId,
            hintLight = TranslationHelper.GetHintLight(q, lang),
            hintMedium = TranslationHelper.GetHintMedium(q, lang),
            hintFull = TranslationHelper.GetHintFull(q, lang),
            explanation = TranslationHelper.GetExplanation(q, lang)
        });

        return Results.Ok(new
        {
            quizId = quizSession.Id,
            questions = mapped
        });
    }

    // Load full question graph in a deterministic order by pre-selected IDs.
    // This avoids empty collection navigations when random ordering + Take is used.
    private static async Task<List<Question>> LoadQuestionsWithDetailsByIds(ApiDbContext db, IReadOnlyList<int> orderedQuestionIds)
    {
        if (orderedQuestionIds.Count == 0)
            return new List<Question>();

        var questions = await db.Questions
            .Where(q => orderedQuestionIds.Contains(q.Id))
            .Include(q => q.Options).ThenInclude(o => o.Translations)
            .Include(q => q.Translations)
            .Include(q => q.Steps).ThenInclude(s => s.Translations)
            .AsSingleQuery()
            .ToListAsync();

        var orderMap = orderedQuestionIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);

        return questions
            .OrderBy(q => orderMap.TryGetValue(q.Id, out var index) ? index : int.MaxValue)
            .ToList();
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

    // 🌍 Helper za resolving user language
    private static async Task<string> ResolveUserLang(ApiDbContext db, HttpContext ctx, int userId)
    {
        var settings = await db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        var acceptLang = ctx.Request.Headers.AcceptLanguage.FirstOrDefault();
        return TranslationHelper.ResolveLanguage(settings?.Language, acceptLang);
    }

    private static int? ParseTopicIdFromTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return null;

        var parts = topic.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        if (int.TryParse(parts[^1], out var parsed) && parsed > 0)
            return parsed;

        return null;
    }

    private static bool TryGetInt(JsonElement json, string property, out int value)
    {
        value = 0;
        if (!json.TryGetProperty(property, out var p))
            return false;

        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out value))
            return true;

        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
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

    private static bool TryGetAnswer(JsonElement json, out string? value)
    {
        value = null;

        // Preferred modern key.
        if (json.TryGetProperty("answer", out var answerNode))
        {
            if (answerNode.ValueKind == JsonValueKind.String)
            {
                value = answerNode.GetString();
                return !string.IsNullOrWhiteSpace(value);
            }

            if (answerNode.ValueKind == JsonValueKind.Number)
            {
                if (answerNode.TryGetInt32(out var answerId) && answerId > 0)
                {
                    value = answerId.ToString();
                    return true;
                }

                var raw = answerNode.GetRawText();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    value = raw.Trim();
                    return true;
                }
            }
        }

        // Legacy client keys.
        foreach (var key in new[] { "selectedOptionId", "answerId", "selectedAnswerId", "optionId" })
        {
            if (TryGetInt(json, key, out var id) && id > 0)
            {
                value = id.ToString();
                return true;
            }
        }

        return false;
    }
}
