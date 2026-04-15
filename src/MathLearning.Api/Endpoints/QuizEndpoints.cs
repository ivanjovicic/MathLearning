using MathLearning.Application.DTOs.Quiz;
using MathLearning.Application.DTOs.Progress;
using MathLearning.Application.Helpers;
using MathLearning.Application.DTOs.AntiCheat;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data;
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
            MathLearning.Api.Services.LegacyStepExplanationAdapter stepAdapter,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
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

            var questionDtos = questions.Select(q => MapQuestionDto(q, lang, stepAdapter)).ToList();

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
            MathLearning.Api.Services.LegacyStepExplanationAdapter stepAdapter,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            string lang = await ResolveUserLang(db, ctx, userId);

            var question = await (
                from q in db.Questions.AsNoTracking()
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

            var steps = NormalizeStepsForResponse(stepAdapter.GetSteps(question, lang));
            var questionText = InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetText(question, lang)) ?? string.Empty;
            var options = question.Options
                .Select(o => new OptionDto(
                    o.Id,
                    InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetOptionText(o, lang)) ?? string.Empty,
                    o.TextFormat,
                    o.RenderMode,
                    TranslationHelper.GetOptionSemanticsAltText(o, lang)))
                .ToList();

            return Results.Ok(new NextQuestionResponse(
                question.Id,
                question.Type,
                questionText,
                options,
                question.Difficulty,
                InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintLight(question, lang)),
                InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintMedium(question, lang)),
                InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintFull(question, lang)),
                InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetExplanation(question, lang)),
                steps,
                question.TextFormat,
                question.ExplanationFormat,
                question.HintFormat,
                question.TextRenderMode,
                question.ExplanationRenderMode,
                question.HintRenderMode,
                TranslationHelper.GetQuestionSemanticsAltText(question, lang)
            ));
        });

        // ✍️ SUBMIT ANSWER
        group.MapPost("/answer", async (
            JsonElement request,
            ApiDbContext db,
            HttpContext ctx,
<<<<<<< HEAD
            MathLearning.Api.Services.LegacyStepExplanationAdapter stepAdapter,
            IQuizAttemptIngestService ingestService,
            IAnswerPatternAntiCheatService antiCheatService) =>
=======
            IQuizAttemptIngestService ingestService,
            XpTrackingService xpTrackingService,
            IOptions<XpTrackingOptions> xpTrackingOptions,
            ILogger<Program> logger) =>
>>>>>>> b6bd21f (feat: harden XP audit pipeline and transactional quiz processing)
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            if (!int.TryParse(userId, out var appUserId))
                return Results.BadRequest("Invalid user id");
            string lang = await ResolveUserLang(db, ctx, userId);

            if (!TryGetInt(request, "questionId", out var questionId) || questionId <= 0)
                return Results.BadRequest("QuestionId is required");
            if (!TryGetAnswer(request, out var answerText) || string.IsNullOrWhiteSpace(answerText))
                return Results.BadRequest("Answer is required");
            if (!TryGetInt(request, "timeSpentSeconds", out var timeSpentSeconds))
                timeSpentSeconds = 0;
            var hintUsed = TryGetBool(request, "hintUsed", out var parsedHintUsed) && parsedHintUsed;
            var clientId = TryGetString(request, "clientId", out var parsedClientId) ? parsedClientId : null;

            var question = await db.Questions
                .AsNoTracking()
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                return Results.NotFound("Question not found");

            Guid quizSessionId;
            string? quizIdRaw = null;
            if (TryGetString(request, "quizId", out var legacyQuizId))
                quizIdRaw = legacyQuizId;
            else if (TryGetString(request, "sessionId", out var sessionId))
                quizIdRaw = sessionId;

            if (!Guid.TryParse(quizIdRaw, out quizSessionId))
            {
                quizSessionId = Guid.NewGuid();
            }
            var answeredAtUtc = DateTime.UtcNow;
            var attemptInput = new AnswerAttemptInput(
                Question: question,
                QuestionId: questionId,
                AnswerText: answerText!,
                TimeSpentSeconds: timeSpentSeconds,
                AnsweredAtUtc: answeredAtUtc,
                IsOffline: false,
                Source: "quiz_answer",
                ClientId: clientId,
                HintUsed: hintUsed,
                QuizSessionId: quizSessionId);

            var processingResult = await ExecuteWithSerializableRetryAsync(
                db,
                logger,
                async () =>
                {
                    await EnsureQuizSessionAsync(db, userId, quizSessionId, answeredAtUtc, ctx.RequestAborted);

                    var result = await ProcessAnswerAttemptWithinTransactionAsync(
                        db,
                        xpTrackingService,
                        userId,
                        attemptInput,
                        xpTrackingOptions.Value.EnableAntiCheat,
                        ctx.RequestAborted);

<<<<<<< HEAD
            stat.LastAttemptAt = DateTime.UtcNow;

            await antiCheatService.EvaluateAndTrackAsync(
                new AntiCheatAnswerObservationInput(
                    userId,
                    "quiz_answer",
                    questionId,
                    null,
                    question.SubtopicId,
                    quizSessionId,
                    null,
                    null,
                    answerText,
                    isCorrect,
                    Math.Max(0, timeSpentSeconds) * 1000,
                    null,
                    answeredAtUtc),
                ctx.RequestAborted);

            await db.SaveChangesAsync();
            await ingestService.IngestAttemptsAsync(
                userId,
                [
                    new QuizAttemptIngestItem(
                        QuizId: quizSessionId,
                        QuestionId: questionId,
                        SubtopicId: question.SubtopicId,
                        Correct: isCorrect,
                        TimeSpentMs: Math.Max(0, timeSpentSeconds) * 1000,
                        CreatedAtUtc: answeredAtUtc)
                ],
=======
                    return new SingleAnswerTransactionResult(result);
                },
>>>>>>> b6bd21f (feat: harden XP audit pipeline and transactional quiz processing)
                ctx.RequestAborted);

            if (processingResult.AttemptResult.IngestItem != null)
            {
                await ingestService.IngestAttemptsAsync(
                    appUserId,
                    [processingResult.AttemptResult.IngestItem],
                    ctx.RequestAborted);
            }

            // Return explanation and steps only on incorrect answer.
            string? explanation = null;
            List<StepExplanationDto>? steps = null;
            if (!processingResult.AttemptResult.IsCorrect)
            {
                var questionForFeedback = await db.Questions
                    .AsNoTracking()
                    .Include(q => q.Translations)
                    .Include(q => q.Steps).ThenInclude(s => s.Translations)
                    .FirstOrDefaultAsync(q => q.Id == questionId);

                if (questionForFeedback != null)
                {
                    explanation = InlineLatexFormatter.NormalizeMixedInlineMath(
                        TranslationHelper.GetExplanation(questionForFeedback, lang));
                    steps = NormalizeStepsForResponse(stepAdapter.GetSteps(questionForFeedback, lang));
                }
            }

            return Results.Ok(new SubmitAnswerResponse(
                processingResult.AttemptResult.IsCorrect,
                explanation,
                steps,
                processingResult.AttemptResult.IsFirstTimeCorrect,
                processingResult.AttemptResult.AwardedXp,
                processingResult.AttemptResult.TotalXpAfterAward
            ));
        });

        // 📤 OFFLINE BATCH SUBMIT (Improved - Idempotent + Server Validation)
        group.MapPost("/offline-submit", async (
            OfflineBatchSubmitRequest request,
            ApiDbContext db,
            HttpContext ctx,
            IQuizAttemptIngestService ingestService,
<<<<<<< HEAD
            IAnswerPatternAntiCheatService antiCheatService) =>
=======
            XpTrackingService xpTrackingService,
            IOptions<XpTrackingOptions> xpTrackingOptions,
            ILogger<Program> logger) =>
>>>>>>> b6bd21f (feat: harden XP audit pipeline and transactional quiz processing)
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            if (!int.TryParse(userId, out var appUserId))
                return Results.BadRequest("Invalid user id");

            if (request.Answers == null || !request.Answers.Any())
                return Results.BadRequest("No answers to import");

            var transactionResult = await ProcessOfflineBatchAsync(
                db,
                xpTrackingService,
                logger,
                userId,
                request.SessionId,
                request.Answers,
                xpTrackingOptions.Value.EnableAntiCheat,
                ctx.RequestAborted);

            if (transactionResult.IngestRows.Count > 0)
                await ingestService.IngestAttemptsAsync(appUserId, transactionResult.IngestRows, ctx.RequestAborted);

<<<<<<< HEAD
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
            var antiCheatInputs = new List<AntiCheatAnswerObservationInput>(request.Answers.Count);
            
            // Batch učitaj sva pitanja odjednom (optimizacija)
            var questionIds = request.Answers.Select(a => a.QuestionId).Distinct().ToList();
            var questions = await db.Questions
                .Include(q => q.Options)
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id);
            var ingestRows = new List<QuizAttemptIngestItem>(request.Answers.Count);
            var existingAnswerKeys = await LoadExistingAnswerKeysAsync(db, userId, request.Answers, ctx.RequestAborted);

            // Batch učitaj postojeće statistike
            var existingStats = await db.UserQuestionStats
                .Where(s => s.UserId == userId && questionIds.Contains(s.QuestionId))
                .ToDictionaryAsync(s => s.QuestionId);

            foreach (var answer in request.Answers)
            {
                var answerKey = BuildAnswerKey(answer.QuestionId, answer.AnsweredAt);
                if (!existingAnswerKeys.Add(answerKey))
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

                ingestRows.Add(new QuizAttemptIngestItem(
                    QuizId: sessionId,
                    QuestionId: answer.QuestionId,
                    SubtopicId: question.SubtopicId,
                    Correct: isCorrectServer,
                    TimeSpentMs: Math.Max(0, answer.TimeSpent) * 1000,
                    CreatedAtUtc: answer.AnsweredAt));

                antiCheatInputs.Add(new AntiCheatAnswerObservationInput(
                    userId,
                    "quiz_offline_submit",
                    answer.QuestionId,
                    null,
                    question.SubtopicId,
                    sessionId,
                    null,
                    null,
                    answer.Answer,
                    isCorrectServer,
                    Math.Max(0, answer.TimeSpent) * 1000,
                    null,
                    answer.AnsweredAt));

                importedCount++;
            }

            
            if (request.Answers.Count > 0)
            {
                var latestDay = request.Answers
                    .Max(a => DateOnly.FromDateTime(a.AnsweredAt));

                var profile = await db.UserProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile != null)
                {
                    if (profile.LastActivityDay == null || latestDay > profile.LastActivityDay)
                        profile.LastActivityDay = latestDay;

                    profile.UpdatedAt = DateTime.UtcNow;
                }
            }

            await antiCheatService.EvaluateAndTrackBatchAsync(antiCheatInputs, ctx.RequestAborted);
            await db.SaveChangesAsync();
            await ingestService.IngestAttemptsAsync(userId, ingestRows, ctx.RequestAborted);

            // Izračunaj fresh XP, Level i Streak
=======
>>>>>>> b6bd21f (feat: harden XP audit pipeline and transactional quiz processing)
            var overview = await CalculateUserOverview(db, userId);

            return Results.Ok(new OfflineBatchSubmitResponse(
                transactionResult.ImportedCount,
                overview.Xp,
                overview.Level,
                overview.Streak
            ));
        });

        // 📤 Legacy alias used by mobile app
        group.MapPost("/batch-submit", async (
            JsonElement payload,
            ApiDbContext db,
            HttpContext ctx,
            IQuizAttemptIngestService ingestService,
<<<<<<< HEAD
            IAnswerPatternAntiCheatService antiCheatService) =>
=======
            XpTrackingService xpTrackingService,
            IOptions<XpTrackingOptions> xpTrackingOptions,
            ILogger<Program> logger) =>
>>>>>>> b6bd21f (feat: harden XP audit pipeline and transactional quiz processing)
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            if (!int.TryParse(userId, out var appUserId))
                return Results.BadRequest("Invalid user id");
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

            var transactionResult = await ProcessOfflineBatchAsync(
                db,
                xpTrackingService,
                logger,
                userId,
                Guid.NewGuid().ToString(),
                answers,
                xpTrackingOptions.Value.EnableAntiCheat,
                ctx.RequestAborted);

<<<<<<< HEAD
            int importedCount = 0;
            var antiCheatInputs = new List<AntiCheatAnswerObservationInput>(answers.Count);
            var questionIds = answers.Select(a => a.QuestionId).Distinct().ToList();
            var questions = await db.Questions
                .Include(q => q.Options)
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id);
            var ingestRows = new List<QuizAttemptIngestItem>(answers.Count);
            var existingAnswerKeys = await LoadExistingAnswerKeysAsync(db, userId, answers, ctx.RequestAborted);
            var existingStats = await db.UserQuestionStats
                .Where(s => s.UserId == userId && questionIds.Contains(s.QuestionId))
                .ToDictionaryAsync(s => s.QuestionId);

            foreach (var answer in answers)
            {
                var answerKey = BuildAnswerKey(answer.QuestionId, answer.AnsweredAt);
                if (!existingAnswerKeys.Add(answerKey))
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

                ingestRows.Add(new QuizAttemptIngestItem(
                    QuizId: sessionGuid,
                    QuestionId: answer.QuestionId,
                    SubtopicId: questionForAnswer.SubtopicId,
                    Correct: isCorrectServer,
                    TimeSpentMs: Math.Max(0, answer.TimeSpent) * 1000,
                    CreatedAtUtc: answer.AnsweredAt));

                antiCheatInputs.Add(new AntiCheatAnswerObservationInput(
                    userId,
                    "quiz_batch_submit",
                    answer.QuestionId,
                    null,
                    questionForAnswer.SubtopicId,
                    sessionGuid,
                    null,
                    null,
                    answer.Answer,
                    isCorrectServer,
                    Math.Max(0, answer.TimeSpent) * 1000,
                    null,
                    answer.AnsweredAt));

                importedCount++;
            }

            
            if (answers.Count > 0)
            {
                var latestDay = answers
                    .Max(a => DateOnly.FromDateTime(a.AnsweredAt));

                var profile = await db.UserProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile != null)
                {
                    if (profile.LastActivityDay == null || latestDay > profile.LastActivityDay)
                        profile.LastActivityDay = latestDay;

                    profile.UpdatedAt = DateTime.UtcNow;
                }
            }

            await antiCheatService.EvaluateAndTrackBatchAsync(antiCheatInputs, ctx.RequestAborted);
            await db.SaveChangesAsync();
            await ingestService.IngestAttemptsAsync(userId, ingestRows, ctx.RequestAborted);
=======
            if (transactionResult.IngestRows.Count > 0)
                await ingestService.IngestAttemptsAsync(appUserId, transactionResult.IngestRows, ctx.RequestAborted);

>>>>>>> b6bd21f (feat: harden XP audit pipeline and transactional quiz processing)
            var overview = await CalculateUserOverview(db, userId);

            return Results.Ok(new OfflineBatchSubmitResponse(
                transactionResult.ImportedCount,
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
            string userId = ctx.User.FindFirst("userId")!.Value;

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
            MathLearning.Api.Services.LegacyStepExplanationAdapter stepAdapter,
            int limit = 20) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            string lang = await ResolveUserLang(db, ctx, userId);

            var dueQuestionIds = await db.QuestionStats
                .AsNoTracking()
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
                    .AsNoTracking()
                    .Where(q => !dueIds.Contains(q.Id))
                    .OrderBy(q => Guid.NewGuid())
                    .Take(needed)
                    .Select(q => q.Id)
                    .ToListAsync();
                var randomFill = await LoadQuestionsWithDetailsByIds(db, randomFillIds);

                questions.AddRange(randomFill);
            }

            return Results.Ok(questions.Select(q => MapQuestionDto(q, lang, stepAdapter)).ToList());
        });

        // 🔀 SRS MIXED (due + random)
        group.MapGet("/srs/mixed", async (
            ApiDbContext db,
            HttpContext ctx,
            MathLearning.Api.Services.LegacyStepExplanationAdapter stepAdapter,
            int count = 15) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            string lang = await ResolveUserLang(db, ctx, userId);

            var dueStats = await db.QuestionStats
                .AsNoTracking()
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
                    .AsNoTracking()
                    .Where(x => !dueIds.Contains(x.Id))
                    .OrderBy(x => Guid.NewGuid())
                    .Take(needed)
                    .Select(x => x.Id)
                    .ToListAsync();
                randomQuestions = await LoadQuestionsWithDetailsByIds(db, randomQuestionIds);
            }

            return Results.Ok(new
            {
                srs = srsQuestions.Select(q => MapQuestionDto(q, lang, stepAdapter)),
                random = randomQuestions.Select(q => MapQuestionDto(q, lang, stepAdapter))
            });
        });

        group.MapGet("/srs/streak", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

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
                }
            }

            return Results.Ok(new
            {
                streak = profile?.Streak ?? 0,
                streakFreezeCount = profile?.StreakFreezeCount ?? 0,
                lastStreakDay = profile?.LastStreakDay,
                lastActivityDay = profile?.LastActivityDay,
                streakEvent = streakEvent
            });
        });
    }

    // 🗺️ Shared helper to map Question entity → QuestionDto with translation + steps
    private static QuestionDto MapQuestionDto(Question q, string lang, MathLearning.Api.Services.LegacyStepExplanationAdapter stepAdapter)
    {
        var options = q.Options
            .Select(o => new OptionDto(
                o.Id,
                InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetOptionText(o, lang)) ?? string.Empty,
                o.TextFormat,
                o.RenderMode,
                TranslationHelper.GetOptionSemanticsAltText(o, lang)))
            .ToList();

        return new QuestionDto(
            q.Id,
            q.Type,
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetText(q, lang)) ?? string.Empty,
            options,
            q.Options.FirstOrDefault(o => o.IsCorrect)?.Id ?? 0,
            q.Difficulty,
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintLight(q, lang)),
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintMedium(q, lang)),
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintFull(q, lang)),
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetExplanation(q, lang)),
            NormalizeStepsForResponse(stepAdapter.GetSteps(q, lang)),
            q.TextFormat,
            q.ExplanationFormat,
            q.HintFormat,
            q.TextRenderMode,
            q.ExplanationRenderMode,
            q.HintRenderMode,
            TranslationHelper.GetQuestionSemanticsAltText(q, lang)
        );
    }

    private static async Task<IResult> BuildLegacyQuestionsResponse(
        ApiDbContext db,
        HttpContext ctx,
        int count,
        int? subtopicId,
        int? topicId)
    {
        string userId = ctx.User.FindFirst("userId")!.Value;
        string lang = await ResolveUserLang(db, ctx, userId);

        IQueryable<Question> query = db.Questions.AsNoTracking();

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
            text = InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetText(q, lang)),
            textFormat = q.TextFormat,
            renderMode = q.TextRenderMode,
            type = q.Type,
            options = q.Options
                .Select(o => new
                {
                    id = o.Id,
                    text = InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetOptionText(o, lang)),
                    textFormat = o.TextFormat,
                    renderMode = o.RenderMode,
                    semanticsAltText = TranslationHelper.GetOptionSemanticsAltText(o, lang)
                })
                .ToList(),
            correctAnswerId = q.Options.FirstOrDefault(o => o.IsCorrect)?.Id ?? 0,
            subtopicId = q.SubtopicId,
            hintLight = InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintLight(q, lang)),
            hintMedium = InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintMedium(q, lang)),
            hintFull = InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintFull(q, lang)),
            explanation = InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetExplanation(q, lang)),
            explanationFormat = q.ExplanationFormat,
            hintFormat = q.HintFormat,
            explanationRenderMode = q.ExplanationRenderMode,
            hintRenderMode = q.HintRenderMode,
            semanticsAltText = TranslationHelper.GetQuestionSemanticsAltText(q, lang)
        });

        return Results.Ok(new
        {
            quizId = quizSession.Id,
            questions = mapped
        });
    }

    private static List<StepExplanationDto> NormalizeStepsForResponse(List<StepExplanationDto> steps)
    {
        return steps
            .Select(step => new StepExplanationDto(
                InlineLatexFormatter.NormalizeMixedInlineMath(step.Text) ?? step.Text,
                InlineLatexFormatter.NormalizeMixedInlineMath(step.Hint),
                step.Highlight,
                step.TextFormat,
                step.HintFormat,
                step.TextRenderMode,
                step.HintRenderMode,
                TranslationHelper.ResolveSemanticsAltText(step.SemanticsAltText, step.Text, step.TextFormat)))
            .ToList();
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
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync();

        var orderMap = orderedQuestionIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);

        return questions
            .OrderBy(q => orderMap.TryGetValue(q.Id, out var index) ? index : int.MaxValue)
            .ToList();
    }

    private static async Task<OfflineBatchTransactionResult> ProcessOfflineBatchAsync(
        ApiDbContext db,
        XpTrackingService xpTrackingService,
        ILogger logger,
        string userId,
        string sessionIdRaw,
        IReadOnlyList<OfflineAnswerDto> answers,
        bool enableAntiCheat,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithSerializableRetryAsync(
            db,
            logger,
            async () =>
            {
                Guid sessionId;
                if (!Guid.TryParse(sessionIdRaw, out sessionId))
                    sessionId = Guid.NewGuid();

                var session = await db.QuizSessions
                    .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);
                if (session == null)
                {
                    session = new QuizSession
                    {
                        Id = sessionId,
                        UserId = userId,
                        StartedAt = answers.Min(a => a.AnsweredAt)
                    };
                    db.QuizSessions.Add(session);
                }

                var questionIds = answers.Select(a => a.QuestionId).Distinct().ToList();
                var questions = await db.Questions
                    .Include(q => q.Options)
                    .Where(q => questionIds.Contains(q.Id))
                    .ToDictionaryAsync(q => q.Id, cancellationToken);

                var imported = 0;
                var ingestRows = new List<QuizAttemptIngestItem>(answers.Count);

                foreach (var answer in answers)
                {
                    if (!questions.TryGetValue(answer.QuestionId, out var question))
                        continue;

                    var result = await ProcessAnswerAttemptWithinTransactionAsync(
                        db,
                        xpTrackingService,
                        userId,
                        new AnswerAttemptInput(
                            question,
                            answer.QuestionId,
                            answer.Answer,
                            answer.TimeSpent,
                            answer.AnsweredAt,
                            true,
                            "offline_submit",
                            null,
                            false,
                            sessionId),
                        enableAntiCheat,
                        cancellationToken);

                    if (!result.WasImported)
                        continue;

                    imported++;
                    if (result.IngestItem != null)
                        ingestRows.Add(result.IngestItem);
                }

                return new OfflineBatchTransactionResult(imported, ingestRows);
            },
            cancellationToken);
    }

    private static async Task<T> ExecuteWithSerializableRetryAsync<T>(
        ApiDbContext db,
        ILogger logger,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await action();
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxAttempts)
            {
                await tx.RollbackAsync(cancellationToken);
                logger.LogWarning(
                    ex,
                    "Retrying serializable transaction after EF concurrency conflict. Attempt={Attempt}/{MaxAttempts}",
                    attempt,
                    maxAttempts);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure && attempt < maxAttempts)
            {
                await tx.RollbackAsync(cancellationToken);
                logger.LogWarning(
                    ex,
                    "Retrying serializable transaction after PostgreSQL serialization failure. Attempt={Attempt}/{MaxAttempts}",
                    attempt,
                    maxAttempts);
            }
            catch (PostgresException ex) when (
                ex.SqlState == PostgresErrorCodes.UniqueViolation &&
                string.Equals(ex.ConstraintName, "UX_UserAnswerAudits_FirstCorrect_PerQuestion", StringComparison.Ordinal) &&
                attempt < maxAttempts)
            {
                await tx.RollbackAsync(cancellationToken);
                logger.LogWarning(
                    ex,
                    "Retrying transaction after first-correct uniqueness conflict. Attempt={Attempt}/{MaxAttempts}",
                    attempt,
                    maxAttempts);
            }
        }

        throw new InvalidOperationException("Failed to process transaction after max retries.");
    }

    private static async Task EnsureQuizSessionAsync(
        ApiDbContext db,
        string userId,
        Guid quizSessionId,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        var existingSession = await db.QuizSessions
            .FirstOrDefaultAsync(s => s.Id == quizSessionId && s.UserId == userId, cancellationToken);

        if (existingSession != null)
            return;

        db.QuizSessions.Add(new QuizSession
        {
            Id = quizSessionId,
            UserId = userId,
            StartedAt = startedAtUtc
        });
    }

    private static async Task<AnswerAttemptResult> ProcessAnswerAttemptWithinTransactionAsync(
        ApiDbContext db,
        XpTrackingService xpTrackingService,
        string userId,
        AnswerAttemptInput input,
        bool enableAntiCheat,
        CancellationToken cancellationToken)
    {
        var existingAnswer = await db.UserAnswers.FirstOrDefaultAsync(x =>
            x.UserId == userId &&
            x.QuestionId == input.QuestionId &&
            x.AnsweredAt == input.AnsweredAtUtc, cancellationToken);

        if (existingAnswer != null)
        {
            var existingAudit = await db.UserAnswerAudits
                .Where(a =>
                    a.UserId == userId &&
                    a.QuestionId == input.QuestionId &&
                    a.AnsweredAt == input.AnsweredAtUtc)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var existingProfileXp = existingAudit?.TotalXpAfterAward ?? await db.UserProfiles
                .Where(p => p.UserId == userId)
                .Select(p => p.Xp)
                .FirstOrDefaultAsync(cancellationToken);

            return new AnswerAttemptResult(
                WasImported: false,
                IsCorrect: existingAnswer.IsCorrect,
                IsFirstTimeCorrect: existingAudit?.IsFirstTimeCorrect ?? false,
                AwardedXp: existingAudit?.AwardedXp ?? 0,
                TotalXpAfterAward: existingProfileXp,
                IngestItem: null);
        }

        var isCorrect = EvaluateAnswerCorrectness(input.Question, input.AnswerText);
        var stat = await GetOrCreateUserQuestionStatForUpdateAsync(db, userId, input.QuestionId, cancellationToken);
        var isFirstTimeCorrect = isCorrect && stat.CorrectAttempts == 0;

        stat.Attempts++;
        if (isCorrect)
            stat.CorrectAttempts++;
        if (stat.LastAttemptAt == null || input.AnsweredAtUtc > stat.LastAttemptAt)
            stat.LastAttemptAt = input.AnsweredAtUtc;

        var profile = await db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null)
            throw new InvalidOperationException($"User profile not found: {userId}");

        var activityDay = DateOnly.FromDateTime(input.AnsweredAtUtc);
        if (profile.LastActivityDay == null || activityDay > profile.LastActivityDay)
            profile.LastActivityDay = activityDay;
        profile.UpdatedAt = DateTime.UtcNow;

        var reason = "not_eligible";
        var requestedXp = 0;
        if (isCorrect && isFirstTimeCorrect)
        {
            requestedXp = 10;
            reason = "awarded";
        }
        else if (isCorrect)
        {
            reason = "already_awarded";
        }

        if (enableAntiCheat && requestedXp > 0 && IsSuspectedCheat(input))
        {
            requestedXp = 0;
            reason = "suspected_cheat";
        }

        XpAwardResult xpAwardResult;
        if (requestedXp > 0)
        {
            xpAwardResult = await xpTrackingService.AddXpWithinTransactionAsync(
                userId,
                requestedXp,
                input.HintUsed,
                input.Source,
                db,
                cancellationToken);
            reason = xpAwardResult.Reason;
        }
        else
        {
            xpAwardResult = new XpAwardResult(0, profile.Xp, reason, 0);
        }

        db.UserAnswers.Add(new UserAnswer
        {
            UserId = userId,
            QuestionId = input.QuestionId,
            QuizSessionId = input.QuizSessionId,
            Answer = input.AnswerText,
            IsCorrect = isCorrect,
            TimeSpentSeconds = input.TimeSpentSeconds,
            AnsweredAt = input.AnsweredAtUtc
        });

        db.UserAnswerAudits.Add(new UserAnswerAudit
        {
            UserId = userId,
            QuestionId = input.QuestionId,
            Source = input.Source,
            IsOffline = input.IsOffline,
            ClientId = input.ClientId,
            Answer = input.AnswerText,
            IsCorrect = isCorrect,
            IsFirstTimeCorrect = isFirstTimeCorrect,
            AwardedXp = xpAwardResult.AwardedXp,
            TotalXpAfterAward = xpAwardResult.TotalXpAfterAward,
            Reason = reason,
            AnsweredAt = input.AnsweredAtUtc,
            CreatedAt = DateTime.UtcNow
        });

        var ingestItem = new QuizAttemptIngestItem(
            QuizId: input.QuizSessionId,
            QuestionId: input.QuestionId,
            SubtopicId: input.Question.SubtopicId,
            Correct: isCorrect,
            TimeSpentMs: Math.Max(0, input.TimeSpentSeconds) * 1000,
            CreatedAtUtc: input.AnsweredAtUtc);

        return new AnswerAttemptResult(
            WasImported: true,
            IsCorrect: isCorrect,
            IsFirstTimeCorrect: isFirstTimeCorrect,
            AwardedXp: xpAwardResult.AwardedXp,
            TotalXpAfterAward: xpAwardResult.TotalXpAfterAward,
            IngestItem: ingestItem);
    }

    private static async Task<UserQuestionStat> GetOrCreateUserQuestionStatForUpdateAsync(
        ApiDbContext db,
        string userId,
        int questionId,
        CancellationToken cancellationToken)
    {
        var existing = await db.UserQuestionStats
            .FromSqlInterpolated($@"SELECT * FROM ""UserQuestionStats"" WHERE ""UserId"" = {userId} AND ""QuestionId"" = {questionId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null)
            return existing;

        var created = new UserQuestionStat
        {
            UserId = userId,
            QuestionId = questionId,
            Attempts = 0,
            CorrectAttempts = 0
        };

        db.UserQuestionStats.Add(created);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException)
        {
            db.Entry(created).State = EntityState.Detached;
            return await db.UserQuestionStats
                .FromSqlInterpolated($@"SELECT * FROM ""UserQuestionStats"" WHERE ""UserId"" = {userId} AND ""QuestionId"" = {questionId} FOR UPDATE")
                .SingleAsync(cancellationToken);
        }
    }

    private static bool EvaluateAnswerCorrectness(Question question, string answerText)
    {
        int.TryParse(answerText, out var selectedOptionId);

        return question.Type == "multiple_choice"
            ? question.Options.Any(o =>
                o.IsCorrect && (
                    o.Text == answerText ||
                    (selectedOptionId > 0 && o.Id == selectedOptionId)))
            : question.CorrectAnswer != null && question.CorrectAnswer.Trim()
                .Equals(answerText.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuspectedCheat(AnswerAttemptInput input)
    {
        if (!input.IsOffline && input.TimeSpentSeconds <= 0)
            return true;

        if (input.IsOffline && input.AnsweredAtUtc > DateTime.UtcNow.AddMinutes(2))
            return true;

        return false;
    }

    // 📊 Helper za računanje XP, Level i Streak
    private static async Task<(int Xp, int Level, int Streak)> CalculateUserOverview(
        ApiDbContext db, 
        string userId)
    {
        var profile = await db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (profile != null)
        {
            var roll = StreakRoller.Apply(profile, today);
            if (roll != null)
                await db.SaveChangesAsync();
        }

        int streak = profile?.Streak ?? 0;
        return (profile?.Xp ?? 0, profile?.Level ?? 1, streak);
    }

    private static async Task<HashSet<string>> LoadExistingAnswerKeysAsync(
        ApiDbContext db,
        string userId,
        IReadOnlyCollection<OfflineAnswerDto> answers,
        CancellationToken cancellationToken)
    {
        if (answers.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var questionIds = answers.Select(x => x.QuestionId).Distinct().ToList();
        var minAnsweredAt = answers.Min(x => x.AnsweredAt);
        var maxAnsweredAt = answers.Max(x => x.AnsweredAt);

        var existing = await db.UserAnswers
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                questionIds.Contains(x.QuestionId) &&
                x.AnsweredAt >= minAnsweredAt &&
                x.AnsweredAt <= maxAnsweredAt)
            .Select(x => new { x.QuestionId, x.AnsweredAt })
            .ToListAsync(cancellationToken);

        return existing
            .Select(x => BuildAnswerKey(x.QuestionId, x.AnsweredAt))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string BuildAnswerKey(int questionId, DateTime answeredAt)
        => $"{questionId}:{answeredAt.Ticks}";

    // 🌍 Helper za resolving user language
    private static async Task<string> ResolveUserLang(ApiDbContext db, HttpContext ctx, string userId)
    {
        var cacheKey = $"req:user-settings:{userId}";
        UserSettings? settings = null;
        if (ctx.Items.TryGetValue(cacheKey, out var cached) && cached is UserSettings cachedSettings)
        {
            settings = cachedSettings;
        }
        else
        {
            settings = await db.UserSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);
            if (settings is not null)
            {
                ctx.Items[cacheKey] = settings;
            }
        }

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

    private sealed record AnswerAttemptInput(
        Question Question,
        int QuestionId,
        string AnswerText,
        int TimeSpentSeconds,
        DateTime AnsweredAtUtc,
        bool IsOffline,
        string Source,
        string? ClientId,
        bool HintUsed,
        Guid QuizSessionId);

    private sealed record AnswerAttemptResult(
        bool WasImported,
        bool IsCorrect,
        bool IsFirstTimeCorrect,
        int AwardedXp,
        int TotalXpAfterAward,
        QuizAttemptIngestItem? IngestItem);

    private sealed record SingleAnswerTransactionResult(AnswerAttemptResult AttemptResult);

    private sealed record OfflineBatchTransactionResult(
        int ImportedCount,
        IReadOnlyList<QuizAttemptIngestItem> IngestRows);
}





