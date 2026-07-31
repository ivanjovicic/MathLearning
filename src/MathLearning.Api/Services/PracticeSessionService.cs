using System.Text.Json;
using MathLearning.Application.DTOs.AntiCheat;
using MathLearning.Application.DTOs.Practice;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Domain.Events;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Persistance.Models;
using MathLearning.Infrastructure.Services.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathLearning.Api.Services;

public sealed class PracticeSessionService : IPracticeSessionService
{
    private static readonly JsonSerializerOptions ReplaySerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiDbContext _db;
    private readonly IQuestionSelector _questionSelector;
    private readonly IBktService _bktService;
    private readonly IPracticeAnalyticsUpdater _analyticsUpdater;
    private readonly IAdaptiveAnalyticsService _adaptiveAnalytics;
    private readonly IAnswerPatternAntiCheatService _antiCheatService;
    private readonly ILogger<PracticeSessionService> _logger;

    public PracticeSessionService(
        ApiDbContext db,
        IQuestionSelector questionSelector,
        IBktService bktService,
        IPracticeAnalyticsUpdater analyticsUpdater,
        IAdaptiveAnalyticsService adaptiveAnalytics,
        IAnswerPatternAntiCheatService antiCheatService,
        ILogger<PracticeSessionService> logger)
    {
        _db = db;
        _questionSelector = questionSelector;
        _bktService = bktService;
        _analyticsUpdater = analyticsUpdater;
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

        var normalizedSelectedOption = request.SelectedOption.Trim();
        var normalizedTimeSpentMs = Math.Max(0, request.TimeSpentMs);
        var replayFingerprint = BuildPracticeAnswerReplayFingerprint(
            userId,
            sessionId,
            request.QuestionId,
            normalizedSelectedOption,
            normalizedTimeSpentMs);

        var session = await _db.PracticeSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, ct);

        if (session is null)
            throw new KeyNotFoundException("Practice session was not found.");

        if (!string.Equals(session.Status, PracticeSessionStatuses.Active, StringComparison.Ordinal))
            throw new InvalidOperationException("Practice session is not active.");

        var latestMatchingItem = await _db.PracticeSessionItems
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId && x.QuestionId == request.QuestionId)
            .OrderByDescending(x => x.PresentedAt)
            .FirstOrDefaultAsync(ct);

        if (latestMatchingItem is null)
            throw new InvalidOperationException("Question does not belong to the current session state.");

        if (latestMatchingItem.AnsweredAt is not null)
            return BuildAnswerReplayResult(latestMatchingItem, replayFingerprint);

        var question = await _db.Questions
            .AsNoTracking()
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == request.QuestionId, ct);

        if (question is null)
            throw new KeyNotFoundException("Question was not found.");

        var isCorrect = EvaluateAnswer(question, normalizedSelectedOption);
        var masteryBefore = latestMatchingItem.BktPrior <= 0
            ? await GetCurrentMasteryAsync(userId, latestMatchingItem.TopicId, latestMatchingItem.SubtopicId, ct)
            : latestMatchingItem.BktPrior;

        var parameters = _bktService.GetParamsForTopic(latestMatchingItem.TopicId);
        var masteryAfter = _bktService.UpdateMastery(masteryBefore, isCorrect, parameters);
        var nowUtc = DateTime.UtcNow;

        var useTransaction = _db.Database.IsRelational();
        IDbContextTransaction? tx = null;
        if (useTransaction)
            tx = await _db.Database.BeginTransactionAsync(ct);

        var claimed = 0;
        if (_db.Database.IsRelational())
        {
            claimed = await _db.PracticeSessionItems
                .Where(x => x.Id == latestMatchingItem.Id && x.AnsweredAt == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.AnsweredAt, nowUtc)
                        .SetProperty(x => x.Correct, isCorrect)
                        .SetProperty(x => x.TimeSpentMs, normalizedTimeSpentMs)
                        .SetProperty(x => x.BktPrior, masteryBefore)
                        .SetProperty(x => x.BktPosterior, masteryAfter)
                        .SetProperty(x => x.SubmissionFingerprintJson, replayFingerprint),
                    ct);
        }
        else
        {
            var trackedItem = await _db.PracticeSessionItems
                .FirstOrDefaultAsync(x => x.Id == latestMatchingItem.Id && x.AnsweredAt == null, ct);

            if (trackedItem is not null)
            {
                trackedItem.AnsweredAt = nowUtc;
                trackedItem.Correct = isCorrect;
                trackedItem.TimeSpentMs = normalizedTimeSpentMs;
                trackedItem.BktPrior = masteryBefore;
                trackedItem.BktPosterior = masteryAfter;
                trackedItem.SubmissionFingerprintJson = replayFingerprint;
                claimed = 1;
            }
        }

        if (claimed == 0)
        {
            if (tx is not null)
                await tx.RollbackAsync(ct);

            var replayItem = await _db.PracticeSessionItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == latestMatchingItem.Id, ct);

            if (replayItem is null)
                throw new InvalidOperationException("Practice session replay payload could not be reloaded.");

            return BuildAnswerReplayResult(replayItem, replayFingerprint);
        }

        var trackedSession = await _db.PracticeSessions
            .Include(x => x.Items)
            .FirstAsync(x => x.Id == sessionId && x.UserId == userId, ct);

        var settledItem = trackedSession.Items.First(x => x.Id == latestMatchingItem.Id);

        trackedSession.AnsweredQuestions += 1;
        if (isCorrect)
            trackedSession.CorrectAnswers += 1;

        var gainedXp = CalculateQuestionXp(isCorrect, settledItem.Difficulty);
        trackedSession.XpEarned += gainedXp;

        await UpsertMasteryStateAsync(
            userId,
            settledItem.TopicId,
            settledItem.SubtopicId,
            masteryAfter,
            nowUtc,
            ct);

        trackedSession.RecommendedDifficulty = DetermineNextDifficulty(trackedSession.Items, masteryAfter);

        await _analyticsUpdater.UpdateAggregatesAsync(
            new PracticeAttemptAnalyticsInput(
                UserId: userId,
                SessionId: sessionId,
                QuestionId: request.QuestionId,
                TopicId: settledItem.TopicId,
                SubtopicId: settledItem.SubtopicId,
                IsCorrect: isCorrect,
                TimeSpentMs: normalizedTimeSpentMs,
                AttemptedAtUtc: nowUtc),
            ct);

        await _antiCheatService.EvaluateAndTrackAsync(
            new AntiCheatAnswerObservationInput(
                userId,
                "practice_session_answer",
                request.QuestionId,
                settledItem.TopicId,
                settledItem.SubtopicId,
                sessionId,
                null,
                null,
                normalizedSelectedOption,
                isCorrect,
                normalizedTimeSpentMs,
                null,
                nowUtc),
            ct);

        var nextQuestion = await BuildNextQuestionAsync(trackedSession, ct);
        var response = new SubmitPracticeAnswerResponse(
            IsCorrect: isCorrect,
            Feedback: isCorrect ? "Correct!" : "Incorrect.",
            MasteryBefore: masteryBefore,
            MasteryAfter: masteryAfter,
            XpEarned: gainedXp,
            NextQuestion: nextQuestion);

        settledItem.SubmissionFingerprintJson = replayFingerprint;
        settledItem.SettledResponseJson = IdempotencyPayloadCanonicalizer.CanonicalizeToJson(response);

        try
        {
            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityTypes = string.Join(", ", ex.Entries.Select(x => x.Metadata.ClrType.Name));
            throw new InvalidOperationException(
                $"Practice session persistence conflict. Entities: {entityTypes}",
                ex);
        }

        _adaptiveAnalytics.TrackEvent("adaptive_answer_submitted", userId, new
        {
            sessionId,
            request.QuestionId,
            isCorrect,
            masteryBefore,
            masteryAfter,
            xpEarned = gainedXp,
            nextDifficulty = trackedSession.RecommendedDifficulty
        });

        return response;
    }

    public async Task<CompletePracticeSessionResponse> CompleteSessionAsync(
        string userId,
        Guid sessionId,
        CancellationToken ct = default)
    {
        var session = await _db.PracticeSessions
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, ct);

        if (session is null)
            throw new KeyNotFoundException("Practice session was not found.");

        if (string.Equals(session.Status, PracticeSessionStatuses.Completed, StringComparison.Ordinal))
            return BuildCompletedSessionReplayResult(session);

        var nowUtc = DateTime.UtcNow;
        var useTransaction = _db.Database.IsRelational();
        IDbContextTransaction? tx = null;
        if (useTransaction)
            tx = await _db.Database.BeginTransactionAsync(ct);

        var claimed = 0;
        if (_db.Database.IsRelational())
        {
            claimed = await _db.PracticeSessions
                .Where(x => x.Id == sessionId && x.UserId == userId && x.Status == PracticeSessionStatuses.Active)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, PracticeSessionStatuses.Completed)
                        .SetProperty(x => x.CompletedAt, nowUtc),
                    ct);
        }
        else
        {
            var trackedSessionForClaim = await _db.PracticeSessions
                .FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId && x.Status == PracticeSessionStatuses.Active, ct);

            if (trackedSessionForClaim is not null)
            {
                trackedSessionForClaim.Status = PracticeSessionStatuses.Completed;
                trackedSessionForClaim.CompletedAt = nowUtc;
                claimed = 1;
            }
        }

        if (claimed == 0)
        {
            if (tx is not null)
                await tx.RollbackAsync(ct);
            var replaySession = await _db.PracticeSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, ct);

            if (replaySession is null)
                throw new KeyNotFoundException("Practice session was not found.");

            if (!string.Equals(replaySession.Status, PracticeSessionStatuses.Completed, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(replaySession.CompletionResponseJson))
            {
                throw new InvalidOperationException("Practice session is not active.");
            }

            return DeserializeCompletePracticeSessionResponse(replaySession.CompletionResponseJson);
        }

        var trackedSession = await _db.PracticeSessions
            .Include(x => x.Items)
            .FirstAsync(x => x.Id == sessionId && x.UserId == userId, ct);

        trackedSession.Status = PracticeSessionStatuses.Completed;
        trackedSession.CompletedAt = nowUtc;

        var accuracy = ComputeAccuracy(trackedSession.CorrectAnswers, trackedSession.AnsweredQuestions);
        if (accuracy >= 0.80m)
            trackedSession.XpEarned += 10;
        trackedSession.XpEarned += 15;

        var finalMastery = await ResolveSessionFinalMasteryAsync(userId, trackedSession, ct);
        trackedSession.FinalMastery = finalMastery;

        await _analyticsUpdater.UpdateDailyActivityAsync(
            userId,
            DateOnly.FromDateTime(nowUtc),
            completed: true,
            ct);

        var nextSkillNodeId = await ResolveRecommendedNextSkillNodeIdAsync(userId, trackedSession.SkillNodeId, ct);
        var response = new CompletePracticeSessionResponse(
            SessionId: trackedSession.Id,
            Status: trackedSession.Status,
            AnsweredQuestions: trackedSession.AnsweredQuestions,
            CorrectAnswers: trackedSession.CorrectAnswers,
            Accuracy: accuracy,
            XpEarned: trackedSession.XpEarned,
            InitialMastery: trackedSession.InitialMastery,
            FinalMastery: finalMastery,
            MasteryDelta: decimal.Round(finalMastery - trackedSession.InitialMastery, 4, MidpointRounding.AwayFromZero),
            WeakTopicsUpdated: true,
            RecommendedNextSkillNodeId: nextSkillNodeId);

        trackedSession.CompletionResponseJson = IdempotencyPayloadCanonicalizer.CanonicalizeToJson(response);

        // Session-keyed outbox identity enforces exactly-one post-session enqueue across crash/retry.
        var postSessionEvent = new PracticePostSessionJobsRequested(userId, sessionId)
        {
            Id = sessionId,
            OccurredUtc = nowUtc
        };
        _db.Outbox.Add(new OutboxMessage
        {
            Id = postSessionEvent.Id,
            OccurredUtc = postSessionEvent.OccurredUtc,
            Type = postSessionEvent.GetType().AssemblyQualifiedName!,
            PayloadJson = JsonSerializer.Serialize(postSessionEvent, postSessionEvent.GetType())
        });

        await _db.SaveChangesAsync(ct);
        if (tx is not null)
            await tx.CommitAsync(ct);

        _adaptiveAnalytics.TrackEvent("adaptive_practice_completed", userId, new
        {
            sessionId,
            trackedSession.AnsweredQuestions,
            trackedSession.CorrectAnswers,
            accuracy,
            trackedSession.XpEarned,
            trackedSession.InitialMastery,
            trackedSession.FinalMastery
        });

        return response;
    }

    private static string BuildPracticeAnswerReplayFingerprint(
        string userId,
        Guid sessionId,
        int questionId,
        string selectedOption,
        int timeSpentMs)
    {
        return IdempotencyPayloadCanonicalizer.CanonicalizeToJson(new
        {
            userId,
            sessionId,
            questionId,
            selectedOption,
            timeSpentMs
        });
    }

    private static SubmitPracticeAnswerResponse BuildAnswerReplayResult(
        PracticeSessionItem item,
        string replayFingerprint)
    {
        if (!string.Equals(item.SubmissionFingerprintJson, replayFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("Practice session item was already answered with a different payload.");

        if (string.IsNullOrWhiteSpace(item.SettledResponseJson))
            throw new InvalidOperationException("Practice session replay payload is missing.");

        return DeserializeSubmitPracticeAnswerResponse(item.SettledResponseJson);
    }

    private static SubmitPracticeAnswerResponse DeserializeSubmitPracticeAnswerResponse(string settledResponseJson)
    {
        return JsonSerializer.Deserialize<SubmitPracticeAnswerResponse>(settledResponseJson, ReplaySerializerOptions)
            ?? throw new InvalidOperationException("Practice session replay payload is missing.");
    }

    private static CompletePracticeSessionResponse BuildCompletedSessionReplayResult(PracticeSession session)
    {
        if (string.IsNullOrWhiteSpace(session.CompletionResponseJson))
            throw new InvalidOperationException("Practice session replay payload is missing.");

        return DeserializeCompletePracticeSessionResponse(session.CompletionResponseJson);
    }

    private static CompletePracticeSessionResponse DeserializeCompletePracticeSessionResponse(string settledResponseJson)
    {
        return JsonSerializer.Deserialize<CompletePracticeSessionResponse>(settledResponseJson, ReplaySerializerOptions)
            ?? throw new InvalidOperationException("Practice session replay payload is missing.");
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
        var candidates = await _db.UserWeaknesses
            .AsNoTracking()
            .Where(x => x.UserId == analyticsUserId)
            .Select(x => new
            {
                x.RecommendedPractice,
                x.Confidence,
                IsHigh = x.WeaknessLevel == WeaknessLevels.High
            })
            .ToListAsync(ct);

        var candidate = candidates
            .OrderByDescending(x => x.Confidence)
            .ThenByDescending(x => x.IsHigh)
            .Select(x => x.RecommendedPractice)
            .FirstOrDefault();

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
        return question.MatchesSubmittedAnswer(selectedOption);
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
