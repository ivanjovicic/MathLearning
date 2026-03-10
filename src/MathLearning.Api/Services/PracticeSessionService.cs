using System.Text.Json;
using MathLearning.Application.DTOs.AntiCheat;
using MathLearning.Application.DTOs.Practice;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

public sealed class PracticeSessionService : IPracticeSessionService
{
    private readonly ApiDbContext _db;
    private readonly IQuestionSelector _questionSelector;
    private readonly IBktService _bktService;
    private readonly IPracticeAnalyticsUpdater _analyticsUpdater;
    private readonly IPracticeBackgroundJobs _backgroundJobs;
    private readonly IAdaptiveAnalyticsService _adaptiveAnalytics;
    private readonly IAnswerPatternAntiCheatService _antiCheatService;
    private readonly ILogger<PracticeSessionService> _logger;

    public PracticeSessionService(
        ApiDbContext db,
        IQuestionSelector questionSelector,
        IBktService bktService,
        IPracticeAnalyticsUpdater analyticsUpdater,
        IPracticeBackgroundJobs backgroundJobs,
        IAdaptiveAnalyticsService adaptiveAnalytics,
        IAnswerPatternAntiCheatService antiCheatService,
        ILogger<PracticeSessionService> logger)
    {
        _db = db;
        _questionSelector = questionSelector;
        _bktService = bktService;
        _analyticsUpdater = analyticsUpdater;
        _backgroundJobs = backgroundJobs;
        _adaptiveAnalytics = adaptiveAnalytics;
        _antiCheatService = antiCheatService;
        _logger = logger;
    }

    public async Task<StartPracticeSessionResponse> StartSessionAsync(
        string userId,
        StartPracticeSessionRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Authenticated user id is required.", nameof(userId));

        if (string.IsNullOrWhiteSpace(request.SkillNodeId) && !request.TopicId.HasValue && !request.SubtopicId.HasValue)
            throw new ArgumentException("At least one of skillNodeId/topicId/subtopicId is required.", nameof(request));

        var nowUtc = DateTime.UtcNow;
        var targetQuestions = Math.Clamp(request.TargetQuestions ?? 10, 1, 25);
        var topicId = request.TopicId;
        var subtopicId = request.SubtopicId;

        if (!topicId.HasValue && subtopicId.HasValue)
        {
            topicId = await _db.Subtopics
                .AsNoTracking()
                .Where(x => x.Id == subtopicId.Value)
                .Select(x => (int?)x.TopicId)
                .FirstOrDefaultAsync(ct);
        }

        var initialMastery = await GetCurrentMasteryAsync(userId, topicId, subtopicId, ct);
        var recommendedDifficulty = string.IsNullOrWhiteSpace(request.PreferredDifficulty)
            ? SelectDifficulty(initialMastery)
            : PracticeDifficulties.Normalize(request.PreferredDifficulty);

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SkillNodeId = request.SkillNodeId,
            TopicId = topicId,
            SubtopicId = subtopicId,
            StartedAt = nowUtc,
            Status = PracticeSessionStatuses.Active,
            TargetQuestions = targetQuestions,
            RecommendedDifficulty = recommendedDifficulty,
            InitialMastery = initialMastery
        };

        var firstQuestion = await _questionSelector.GetNextQuestionAsync(
            new QuestionSelectionCriteria(
                TopicId: topicId,
                SubtopicId: subtopicId,
                Difficulty: recommendedDifficulty,
                ExcludedQuestionIds: [],
                Take: 1),
            ct);

        PracticeQuestionDto? firstQuestionDto = null;
        if (firstQuestion is not null)
        {
            session.TopicId ??= firstQuestion.TopicId;
            session.SubtopicId ??= firstQuestion.SubtopicId;

            var firstPrior = await GetCurrentMasteryAsync(
                userId,
                firstQuestion.TopicId,
                firstQuestion.SubtopicId,
                ct);

            session.Items.Add(new PracticeSessionItem
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = firstQuestion.Id,
                TopicId = firstQuestion.TopicId,
                SubtopicId = firstQuestion.SubtopicId,
                Difficulty = firstQuestion.Difficulty,
                PresentedAt = nowUtc,
                AttemptNumber = 1,
                BktPrior = firstPrior,
                BktPosterior = firstPrior
            });

            firstQuestionDto = ToQuestionDto(firstQuestion);
        }

        _db.PracticeSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        _adaptiveAnalytics.TrackEvent("adaptive_practice_started", userId, new
        {
            sessionId = session.Id,
            topicId = session.TopicId,
            subtopicId = session.SubtopicId,
            targetQuestions = session.TargetQuestions,
            recommendedDifficulty = session.RecommendedDifficulty
        });

        return new StartPracticeSessionResponse(
            SessionId: session.Id,
            SkillNodeId: session.SkillNodeId,
            RecommendedDifficulty: session.RecommendedDifficulty,
            InitialMastery: session.InitialMastery,
            Question: firstQuestionDto);
    }

    public async Task<SubmitPracticeAnswerResponse> SubmitAnswerAsync(
        string userId,
        Guid sessionId,
        SubmitPracticeAnswerRequest request,
        CancellationToken ct = default)
    {
        if (request.QuestionId <= 0)
            throw new ArgumentException("questionId must be a positive integer.", nameof(request));

        var session = await _db.PracticeSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, ct);

        if (session is null)
            throw new KeyNotFoundException("Practice session was not found.");

        if (!string.Equals(session.Status, PracticeSessionStatuses.Active, StringComparison.Ordinal))
            throw new InvalidOperationException("Practice session is not active.");

        var sessionItems = await _db.PracticeSessionItems
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.PresentedAt)
            .ToListAsync(ct);

        var latestMatchingItem = sessionItems
            .Where(x => x.QuestionId == request.QuestionId)
            .OrderByDescending(x => x.PresentedAt)
            .FirstOrDefault();

        if (latestMatchingItem is null)
            throw new InvalidOperationException("Question does not belong to the current session state.");

        if (latestMatchingItem.AnsweredAt is not null)
        {
            var queued = await BuildNextQuestionAsync(session, ct);
            return new SubmitPracticeAnswerResponse(
                IsCorrect: latestMatchingItem.Correct ?? false,
                Feedback: (latestMatchingItem.Correct ?? false) ? "Correct!" : "Incorrect.",
                MasteryBefore: latestMatchingItem.BktPrior,
                MasteryAfter: latestMatchingItem.BktPosterior,
                XpEarned: 0,
                NextQuestion: queued);
        }

        var question = await _db.Questions
            .AsNoTracking()
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == request.QuestionId, ct);

        if (question is null)
            throw new KeyNotFoundException("Question was not found.");

        var isCorrect = EvaluateAnswer(question, request.SelectedOption);
        var masteryBefore = latestMatchingItem.BktPrior <= 0
            ? await GetCurrentMasteryAsync(userId, latestMatchingItem.TopicId, latestMatchingItem.SubtopicId, ct)
            : latestMatchingItem.BktPrior;

        var parameters = _bktService.GetParamsForTopic(latestMatchingItem.TopicId);
        var masteryAfter = _bktService.UpdateMastery(masteryBefore, isCorrect, parameters);
        var nowUtc = DateTime.UtcNow;

        var transactionEnabled = _db.Database.IsRelational();
        await using var tx = transactionEnabled
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        latestMatchingItem.AnsweredAt = nowUtc;
        latestMatchingItem.Correct = isCorrect;
        latestMatchingItem.TimeSpentMs = Math.Max(0, request.TimeSpentMs);
        latestMatchingItem.BktPrior = masteryBefore;
        latestMatchingItem.BktPosterior = masteryAfter;

        session.AnsweredQuestions += 1;
        if (isCorrect)
            session.CorrectAnswers += 1;

        var gainedXp = CalculateQuestionXp(isCorrect, latestMatchingItem.Difficulty);
        session.XpEarned += gainedXp;

        await UpsertMasteryStateAsync(
            userId,
            latestMatchingItem.TopicId,
            latestMatchingItem.SubtopicId,
            masteryAfter,
            nowUtc,
            ct);

        session.RecommendedDifficulty = DetermineNextDifficulty(sessionItems, masteryAfter);

        await _analyticsUpdater.UpdateAggregatesAsync(
            new PracticeAttemptAnalyticsInput(
                UserId: userId,
                SessionId: sessionId,
                QuestionId: request.QuestionId,
                TopicId: latestMatchingItem.TopicId,
                SubtopicId: latestMatchingItem.SubtopicId,
                IsCorrect: isCorrect,
                TimeSpentMs: Math.Max(0, request.TimeSpentMs),
                AttemptedAtUtc: nowUtc),
            ct);

        await _antiCheatService.EvaluateAndTrackAsync(
            new AntiCheatAnswerObservationInput(
                userId,
                "practice_session_answer",
                request.QuestionId,
                latestMatchingItem.TopicId,
                latestMatchingItem.SubtopicId,
                sessionId,
                null,
                null,
                request.SelectedOption,
                isCorrect,
                Math.Max(0, request.TimeSpentMs),
                null,
                nowUtc),
            ct);

        var nextQuestion = await BuildNextQuestionAsync(session, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityTypes = string.Join(", ", ex.Entries.Select(x => x.Metadata.ClrType.Name));
            throw new InvalidOperationException(
                $"Practice session persistence conflict. Entities: {entityTypes}",
                ex);
        }
        if (tx is not null)
            await tx.CommitAsync(ct);

        _adaptiveAnalytics.TrackEvent("adaptive_answer_submitted", userId, new
        {
            sessionId,
            request.QuestionId,
            isCorrect,
            masteryBefore,
            masteryAfter,
            xpEarned = gainedXp,
            nextDifficulty = session.RecommendedDifficulty
        });

        return new SubmitPracticeAnswerResponse(
            IsCorrect: isCorrect,
            Feedback: isCorrect ? "Correct!" : "Incorrect.",
            MasteryBefore: masteryBefore,
            MasteryAfter: masteryAfter,
            XpEarned: gainedXp,
            NextQuestion: nextQuestion);
    }

    public async Task<CompletePracticeSessionResponse> CompleteSessionAsync(
        string userId,
        Guid sessionId,
        CancellationToken ct = default)
    {
        var session = await _db.PracticeSessions
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, ct);

        if (session is null)
            throw new KeyNotFoundException("Practice session was not found.");

        if (string.Equals(session.Status, PracticeSessionStatuses.Completed, StringComparison.Ordinal))
        {
            var finalMasteryCompleted = session.FinalMastery ?? session.InitialMastery;
            return new CompletePracticeSessionResponse(
                SessionId: session.Id,
                Status: session.Status,
                AnsweredQuestions: session.AnsweredQuestions,
                CorrectAnswers: session.CorrectAnswers,
                Accuracy: ComputeAccuracy(session.CorrectAnswers, session.AnsweredQuestions),
                XpEarned: session.XpEarned,
                InitialMastery: session.InitialMastery,
                FinalMastery: finalMasteryCompleted,
                MasteryDelta: decimal.Round(finalMasteryCompleted - session.InitialMastery, 4, MidpointRounding.AwayFromZero),
                WeakTopicsUpdated: true,
                RecommendedNextSkillNodeId: await ResolveRecommendedNextSkillNodeIdAsync(userId, session.SkillNodeId, ct));
        }

        var nowUtc = DateTime.UtcNow;
        var accuracy = ComputeAccuracy(session.CorrectAnswers, session.AnsweredQuestions);
        if (accuracy >= 0.80m)
            session.XpEarned += 10;
        session.XpEarned += 15;

        session.Status = PracticeSessionStatuses.Completed;
        session.CompletedAt = nowUtc;

        var finalMastery = await ResolveSessionFinalMasteryAsync(userId, session, ct);
        session.FinalMastery = finalMastery;

        await _analyticsUpdater.UpdateDailyActivityAsync(
            userId,
            DateOnly.FromDateTime(nowUtc),
            completed: true,
            ct);

        await _db.SaveChangesAsync(ct);
        await _backgroundJobs.EnqueuePostSessionJobsAsync(userId, ct);

        var nextSkillNodeId = await ResolveRecommendedNextSkillNodeIdAsync(userId, session.SkillNodeId, ct);

        _adaptiveAnalytics.TrackEvent("adaptive_practice_completed", userId, new
        {
            sessionId,
            session.AnsweredQuestions,
            session.CorrectAnswers,
            accuracy,
            session.XpEarned,
            session.InitialMastery,
            session.FinalMastery
        });

        return new CompletePracticeSessionResponse(
            SessionId: session.Id,
            Status: session.Status,
            AnsweredQuestions: session.AnsweredQuestions,
            CorrectAnswers: session.CorrectAnswers,
            Accuracy: accuracy,
            XpEarned: session.XpEarned,
            InitialMastery: session.InitialMastery,
            FinalMastery: finalMastery,
            MasteryDelta: decimal.Round(finalMastery - session.InitialMastery, 4, MidpointRounding.AwayFromZero),
            WeakTopicsUpdated: true,
            RecommendedNextSkillNodeId: nextSkillNodeId);
    }

    private async Task UpsertMasteryStateAsync(
        string userId,
        int topicId,
        int? subtopicId,
        decimal masteryAfter,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var row = await _db.MasteryStates
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.TopicId == topicId &&
                x.SubtopicId == subtopicId, ct);

        if (row is null)
        {
            row = new MasteryState
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TopicId = topicId,
                SubtopicId = subtopicId
            };
            _db.MasteryStates.Add(row);
        }

        row.PL = decimal.Round(Math.Clamp(masteryAfter, 0m, 1m), 4, MidpointRounding.AwayFromZero);
        row.UpdatedAt = nowUtc;
    }

    private async Task<PracticeQuestionDto?> BuildNextQuestionAsync(PracticeSession session, CancellationToken ct)
    {
        if (session.AnsweredQuestions >= session.TargetQuestions)
            return null;

        var excluded = await _db.PracticeSessionItems
            .AsNoTracking()
            .Where(x => x.SessionId == session.Id)
            .Select(x => x.QuestionId)
            .Distinct()
            .ToListAsync(ct);
        var next = await _questionSelector.GetNextQuestionAsync(
            new QuestionSelectionCriteria(
                TopicId: session.TopicId,
                SubtopicId: session.SubtopicId,
                Difficulty: session.RecommendedDifficulty,
                ExcludedQuestionIds: excluded,
                Take: 1),
            ct);

        if (next is null)
            return null;

        var prior = await GetCurrentMasteryAsync(session.UserId, next.TopicId, next.SubtopicId, ct);
        _db.PracticeSessionItems.Add(new PracticeSessionItem
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = next.Id,
            TopicId = next.TopicId,
            SubtopicId = next.SubtopicId,
            Difficulty = next.Difficulty,
            PresentedAt = DateTime.UtcNow,
            AttemptNumber = 1,
            BktPrior = prior,
            BktPosterior = prior
        });

        session.TopicId ??= next.TopicId;
        session.SubtopicId ??= next.SubtopicId;
        return ToQuestionDto(next);
    }

    private async Task<decimal> GetCurrentMasteryAsync(
        string userId,
        int? topicId,
        int? subtopicId,
        CancellationToken ct)
    {
        if (!topicId.HasValue)
            return 0.20m;

        var row = await _db.MasteryStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.TopicId == topicId.Value &&
                x.SubtopicId == subtopicId, ct);

        if (row is not null)
            return decimal.Round(Math.Clamp(row.PL, 0m, 1m), 4, MidpointRounding.AwayFromZero);

        return _bktService.GetParamsForTopic(topicId.Value).PL0;
    }

    private async Task<decimal> ResolveSessionFinalMasteryAsync(string userId, PracticeSession session, CancellationToken ct)
    {
        var topicIds = await _db.PracticeSessionItems
            .AsNoTracking()
            .Where(x => x.SessionId == session.Id)
            .Select(x => x.TopicId)
            .Distinct()
            .ToListAsync(ct);
        if (topicIds.Count == 0)
            return session.InitialMastery;

        var rows = await _db.MasteryStates
            .AsNoTracking()
            .Where(x => x.UserId == userId && topicIds.Contains(x.TopicId))
            .Select(x => x.PL)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return session.InitialMastery;

        return decimal.Round(rows.Average(), 4, MidpointRounding.AwayFromZero);
    }

    private async Task<string?> ResolveRecommendedNextSkillNodeIdAsync(
        string userId,
        string? currentSkillNodeId,
        CancellationToken ct)
    {
        var analyticsUserId = MathLearning.Application.Helpers.UserIdGuidMapper.FromIdentityUserId(userId);
        var candidate = await _db.UserWeaknesses
            .AsNoTracking()
            .Where(x => x.UserId == analyticsUserId)
            .OrderByDescending(x => x.Confidence)
            .ThenByDescending(x => x.WeaknessLevel == WeaknessLevels.High)
            .Select(x => x.RecommendedPractice)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(candidate);
            if (doc.RootElement.TryGetProperty("id", out var idNode))
            {
                var id = idNode.GetString();
                if (!string.IsNullOrWhiteSpace(id) && !string.Equals(id, currentSkillNodeId, StringComparison.OrdinalIgnoreCase))
                    return id;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed JSON payloads in legacy rows.
        }

        return null;
    }

    private static bool EvaluateAnswer(Question question, string selectedOption)
    {
        var answer = selectedOption?.Trim() ?? string.Empty;
        if (answer.Length == 0)
            return false;

        if (string.Equals(question.Type, "multiple_choice", StringComparison.OrdinalIgnoreCase))
        {
            var asInt = int.TryParse(answer, out var optionId) ? optionId : -1;
            return question.Options.Any(o =>
                o.IsCorrect &&
                (o.Id == asInt || string.Equals(o.Text.Trim(), answer, StringComparison.OrdinalIgnoreCase)));
        }

        return question.CorrectAnswer is not null &&
               string.Equals(question.CorrectAnswer.Trim(), answer, StringComparison.OrdinalIgnoreCase);
    }

    private static string SelectDifficulty(decimal mastery) =>
        mastery switch
        {
            < 0.40m => PracticeDifficulties.Easy,
            <= 0.70m => PracticeDifficulties.Medium,
            _ => PracticeDifficulties.Hard
        };

    private static string DetermineNextDifficulty(IEnumerable<PracticeSessionItem> items, decimal masteryAfter)
    {
        var difficulty = SelectDifficulty(masteryAfter);
        var answered = items
            .Where(x => x.AnsweredAt is not null && x.Correct is not null)
            .OrderByDescending(x => x.AnsweredAt)
            .ToList();

        var consecutiveIncorrect = 0;
        foreach (var item in answered)
        {
            if (item.Correct == true)
                break;
            consecutiveIncorrect++;
        }

        if (consecutiveIncorrect >= 2)
            return Demote(difficulty);

        var consecutiveCorrect = 0;
        foreach (var item in answered)
        {
            if (item.Correct != true)
                break;
            consecutiveCorrect++;
        }

        if (consecutiveCorrect >= 3)
            return Promote(difficulty);

        return difficulty;
    }

    private static string Promote(string difficulty) =>
        difficulty switch
        {
            PracticeDifficulties.Easy => PracticeDifficulties.Medium,
            PracticeDifficulties.Medium => PracticeDifficulties.Hard,
            _ => PracticeDifficulties.Hard
        };

    private static string Demote(string difficulty) =>
        difficulty switch
        {
            PracticeDifficulties.Hard => PracticeDifficulties.Medium,
            PracticeDifficulties.Medium => PracticeDifficulties.Easy,
            _ => PracticeDifficulties.Easy
        };

    private static int CalculateQuestionXp(bool isCorrect, string difficulty)
    {
        if (!isCorrect)
            return 0;

        return PracticeDifficulties.Normalize(difficulty) switch
        {
            PracticeDifficulties.Easy => 5,
            PracticeDifficulties.Medium => 8,
            PracticeDifficulties.Hard => 12,
            _ => 8
        };
    }

    private static decimal ComputeAccuracy(int correct, int total)
    {
        if (total <= 0)
            return 0m;

        return decimal.Round((decimal)correct / total, 4, MidpointRounding.AwayFromZero);
    }

    private static PracticeQuestionDto ToQuestionDto(SelectedQuestion question) =>
        new(
            Id: question.Id,
            Prompt: question.Prompt,
            Options: question.Options
                .Select(x => new PracticeQuestionOptionDto(
                    x.Id,
                    x.Text,
                    x.TextFormat,
                    x.RenderMode,
                    TranslationHelper.ResolveSemanticsAltText(x.SemanticsAltText, x.Text, x.TextFormat)))
                .ToList(),
            Difficulty: question.Difficulty,
            PromptFormat: question.PromptFormat,
            RenderMode: question.RenderMode,
            SemanticsAltText: TranslationHelper.ResolveSemanticsAltText(
                question.SemanticsAltText,
                question.Prompt,
                question.PromptFormat));
}
