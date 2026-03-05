using MathLearning.Application.DTOs.Quiz;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

public sealed class AdaptiveLearningService : IAdaptiveLearningService
{
    private const int SessionQuestionCount = 10;
    private const int WeakQuestionCount = 5;
    private const int RecentQuestionCount = 3;
    private const int ReviewQuestionCount = 2;

    private const int WeakTopicAttemptThreshold = 6;
    private const double WeakTopicAccuracyThreshold = 0.60;
    private const int RecentHistoryExcludeCount = 60;
    private const int HardSequenceLimit = 2;

    private static readonly TimeSpan RecommendationCacheTtl = TimeSpan.FromMinutes(5);

    private readonly ApiDbContext _db;
    private readonly ISrsService _srsService;
    private readonly InMemoryCacheService _cache;
    private readonly ILogger<AdaptiveLearningService> _logger;

    public AdaptiveLearningService(
        ApiDbContext db,
        ISrsService srsService,
        InMemoryCacheService cache,
        ILogger<AdaptiveLearningService> logger)
    {
        _db = db;
        _srsService = srsService;
        _cache = cache;
        _logger = logger;
    }

    public Task<AdaptiveSession> GeneratePracticeSessionAsync(string userId) =>
        GeneratePracticeSessionAsync(userId, CancellationToken.None);

    public Task<AdaptiveAnswerResult> SubmitAnswerAsync(string userId, AdaptiveAnswerRequest request) =>
        SubmitAnswerAsync(userId, request, CancellationToken.None);

    public Task<List<AdaptiveRecommendation>> GetRecommendationsAsync(string userId) =>
        GetRecommendationsAsync(userId, CancellationToken.None);

    public Task<List<ReviewItem>> GetDueReviewsAsync(string userId) =>
        GetDueReviewsAsync(userId, CancellationToken.None);

    public Task DetectWeakTopicsAsync(string userId) =>
        DetectWeakTopicsAsync(userId, CancellationToken.None);

    private async Task<AdaptiveSession> GeneratePracticeSessionAsync(string userId, CancellationToken ct)
    {
        ValidateUserId(userId);
        var nowUtc = DateTime.UtcNow;

        var profile = await EnsureLearningProfileAsync(userId, nowUtc, ct);

        var weakTopicIds = await _db.UserTopicMasteries
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.IsWeak)
            .OrderBy(m => m.MasteryScore)
            .ThenByDescending(m => m.Attempts)
            .Select(m => m.TopicId)
            .Take(6)
            .ToListAsync(ct);

        var recentTopicIds = await BuildRecentTopicQuery(userId)
            .Take(6)
            .Select(x => x.TopicId)
            .ToListAsync(ct);

        var excludedQuestionIds = await _db.UserQuestionHistories
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.AnsweredAt)
            .Take(RecentHistoryExcludeCount)
            .Select(h => h.QuestionId)
            .Distinct()
            .ToListAsync(ct);

        var usedQuestionIds = new HashSet<int>(excludedQuestionIds);

        var reviewCandidates = await GetReviewCandidatesAsync(userId, nowUtc, usedQuestionIds, ct);
        var weakCandidates = await GetTopicCandidatesAsync(
            userId,
            weakTopicIds,
            profile.PreferredDifficulty,
            "weak",
            usedQuestionIds,
            WeakQuestionCount * 4,
            ct);
        var recentCandidates = await GetTopicCandidatesAsync(
            userId,
            recentTopicIds,
            profile.PreferredDifficulty,
            "recent",
            usedQuestionIds,
            RecentQuestionCount * 4,
            ct);

        var selected = new List<QuestionCandidate>(SessionQuestionCount);
        SelectCandidates(selected, reviewCandidates, ReviewQuestionCount, usedQuestionIds);
        SelectCandidates(selected, weakCandidates, WeakQuestionCount, usedQuestionIds);
        SelectCandidates(selected, recentCandidates, RecentQuestionCount, usedQuestionIds);

        if (selected.Count < SessionQuestionCount)
        {
            var fallbackCandidates = await GetFallbackCandidatesAsync(
                userId,
                profile.PreferredDifficulty,
                usedQuestionIds,
                SessionQuestionCount - selected.Count,
                ct);

            SelectCandidates(selected, fallbackCandidates, SessionQuestionCount - selected.Count, usedQuestionIds);
        }

        var ordered = BuildDifficultyAwareSequence(selected);

        var session = new AdaptiveSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = nowUtc,
            ExpiresAt = nowUtc.AddMinutes(35),
            IsCompleted = false,
            ProfileDifficulty = profile.PreferredDifficulty,
            Items = ordered
                .Select((candidate, index) => new AdaptiveSessionItem
                {
                    Id = Guid.NewGuid(),
                    AdaptiveSessionId = Guid.Empty,
                    QuestionId = candidate.QuestionId,
                    TopicId = candidate.TopicId,
                    SubtopicId = candidate.SubtopicId,
                    SourceType = candidate.Source,
                    DifficultyLevel = candidate.DifficultyLevel,
                    Sequence = index + 1,
                    CreatedAt = nowUtc
                })
                .ToList()
        };

        foreach (var item in session.Items)
            item.AdaptiveSessionId = session.Id;

        _db.AdaptiveSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Adaptive session generated. UserId={UserId} SessionId={SessionId} Items={ItemCount} WeakCandidates={WeakCandidates} RecentCandidates={RecentCandidates} ReviewCandidates={ReviewCandidates}",
            userId,
            session.Id,
            session.Items.Count,
            weakCandidates.Count,
            recentCandidates.Count,
            reviewCandidates.Count);

        return session;
    }

    private async Task<AdaptiveAnswerResult> SubmitAnswerAsync(
        string userId,
        AdaptiveAnswerRequest request,
        CancellationToken ct)
    {
        ValidateUserId(userId);
        ValidateAnswerRequest(request);

        var nowUtc = DateTime.UtcNow;
        var answeredAt = request.AnsweredAt ?? nowUtc;
        var boundedConfidence = Math.Clamp(request.Confidence, 0d, 1d);
        var boundedResponseTime = Math.Max(0, request.ResponseTimeSeconds);

        var sessionItem = await _db.AdaptiveSessionItems
            .Include(i => i.AdaptiveSession)
            .FirstOrDefaultAsync(
                i => i.Id == request.AdaptiveSessionItemId &&
                     i.AdaptiveSessionId == request.AdaptiveSessionId,
                ct);

        if (sessionItem is null || sessionItem.AdaptiveSession is null || sessionItem.AdaptiveSession.UserId != userId)
            throw new InvalidOperationException("Adaptive session item not found for the user.");

        if (sessionItem.QuestionId != request.QuestionId)
            throw new InvalidOperationException("Question mismatch for adaptive session item.");

        var question = await _db.Questions
            .Include(q => q.Options)
            .Include(q => q.Subtopic)
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, ct);

        if (question is null || question.Subtopic is null)
            throw new InvalidOperationException("Question or taxonomy metadata not found.");

        var isCorrect = EvaluateAnswer(question, request.Answer);

        sessionItem.IsCorrect = isCorrect;
        sessionItem.Confidence = boundedConfidence;
        sessionItem.ResponseTimeSeconds = boundedResponseTime;
        sessionItem.AnsweredAt = answeredAt;

        var learningProfile = await EnsureLearningProfileAsync(userId, nowUtc, ct);
        var difficultyDecision = UpdateLearningProfileAndDifficulty(
            learningProfile,
            isCorrect,
            boundedResponseTime,
            nowUtc);

        var historyEntry = new UserQuestionHistory
        {
            UserId = userId,
            QuestionId = question.Id,
            TopicId = question.Subtopic.TopicId,
            SubtopicId = question.SubtopicId,
            IsCorrect = isCorrect,
            Confidence = boundedConfidence,
            ResponseTimeSeconds = boundedResponseTime,
            DifficultyLevel = AdaptiveDifficultyMapper.FromQuestionDifficulty(question.Difficulty),
            AnsweredAt = answeredAt,
            AdaptiveSessionId = request.AdaptiveSessionId,
            AdaptiveSessionItemId = request.AdaptiveSessionItemId
        };

        _db.UserQuestionHistories.Add(historyEntry);

        var reviewSchedule = await UpsertReviewScheduleAsync(
            userId,
            question.Id,
            question.Subtopic.TopicId,
            isCorrect,
            boundedConfidence,
            nowUtc,
            ct);

        await _db.SaveChangesAsync(ct);

        var hasPendingItems = await _db.AdaptiveSessionItems
            .AsNoTracking()
            .AnyAsync(i => i.AdaptiveSessionId == request.AdaptiveSessionId && i.IsCorrect == null, ct);

        sessionItem.AdaptiveSession.IsCompleted = !hasPendingItems;

        var mastery = await RecalculateTopicMasteryAsync(userId, question.Subtopic.TopicId, nowUtc, ct);
        mastery.DifficultyLevel = learningProfile.PreferredDifficulty;
        await _db.SaveChangesAsync(ct);

        await TryUpdateLegacySrsAsync(userId, question.Id, isCorrect, boundedResponseTime);
        await _cache.RemoveAsync(GetRecommendationCacheKey(userId));

        _logger.LogInformation(
            "Adaptive answer submitted. UserId={UserId} SessionId={SessionId} SessionItemId={SessionItemId} QuestionId={QuestionId} Correct={IsCorrect} Mastery={Mastery} NextReviewAt={NextReviewAt}",
            userId,
            request.AdaptiveSessionId,
            request.AdaptiveSessionItemId,
            question.Id,
            isCorrect,
            mastery.MasteryScore,
            reviewSchedule.DueAt);

        return new AdaptiveAnswerResult
        {
            IsCorrect = isCorrect,
            DifficultyLevel = learningProfile.PreferredDifficulty,
            WasDifficultyAdjusted = difficultyDecision.Changed,
            TopicId = question.Subtopic.TopicId,
            TopicMasteryScore = mastery.MasteryScore,
            IsWeakTopic = mastery.IsWeak,
            NextReviewAt = reviewSchedule.DueAt,
            ReviewIntervalDays = reviewSchedule.IntervalDays,
            ReviewEasinessFactor = reviewSchedule.EasinessFactor,
            Explanation = !isCorrect ? question.Explanation : null
        };
    }

    private async Task<List<AdaptiveRecommendation>> GetRecommendationsAsync(string userId, CancellationToken ct)
    {
        ValidateUserId(userId);

        var cacheKey = GetRecommendationCacheKey(userId);
        var cached = await _cache.GetAsync<List<AdaptiveRecommendation>>(cacheKey);
        if (cached is { Count: > 0 })
            return cached;

        var nowUtc = DateTime.UtcNow;
        var recommendations = new List<AdaptiveRecommendation>();

        var weakTopics = await (
            from mastery in _db.UserTopicMasteries.AsNoTracking()
            join topic in _db.Topics.AsNoTracking()
                on mastery.TopicId equals topic.Id
            where mastery.UserId == userId && mastery.IsWeak
            orderby mastery.MasteryScore, mastery.Attempts descending
            select new TopicMastery
            {
                TopicId = mastery.TopicId,
                Topic = topic.Name,
                MasteryScore = mastery.MasteryScore,
                Attempts = mastery.Attempts,
                Accuracy = mastery.Attempts == 0 ? 0 : (double)mastery.CorrectAttempts / mastery.Attempts,
                IsWeak = mastery.IsWeak,
                Difficulty = mastery.DifficultyLevel,
                LastPracticedAt = mastery.LastPracticedAt
            })
            .Take(3)
            .ToListAsync(ct);

        foreach (var weak in weakTopics)
        {
            recommendations.Add(new AdaptiveRecommendation
            {
                TopicId = weak.TopicId,
                Topic = weak.Topic,
                Difficulty = weak.Difficulty,
                QuestionCount = 5,
                Confidence = Math.Round(Math.Clamp(1d - (weak.MasteryScore / 100d), 0.2d, 0.95d), 2),
                Reason = "Low accuracy and mastery trend indicate reinforcement is needed."
            });
        }

        var dueByTopic = await (
            from review in _db.ReviewSchedules.AsNoTracking()
            join topic in _db.Topics.AsNoTracking()
                on review.TopicId equals topic.Id
            where review.UserId == userId && review.DueAt <= nowUtc
            group review by new { review.TopicId, topic.Name } into g
            orderby g.Count() descending
            select new
            {
                g.Key.TopicId,
                Topic = g.Key.Name,
                DueCount = g.Count()
            })
            .Take(3)
            .ToListAsync(ct);

        foreach (var due in dueByTopic)
        {
            if (recommendations.Any(r => r.TopicId == due.TopicId))
                continue;

            recommendations.Add(new AdaptiveRecommendation
            {
                TopicId = due.TopicId,
                Topic = due.Topic,
                Difficulty = AdaptiveDifficultyLevels.Medium,
                QuestionCount = Math.Min(8, Math.Max(2, due.DueCount)),
                Confidence = Math.Round(Math.Clamp(due.DueCount / 10d, 0.4d, 0.9d), 2),
                Reason = "Spaced repetition items are due and should be reviewed now."
            });
        }

        if (recommendations.Count < 3)
        {
            var additional = await (
                from mastery in _db.UserTopicMasteries.AsNoTracking()
                join topic in _db.Topics.AsNoTracking()
                    on mastery.TopicId equals topic.Id
                where mastery.UserId == userId && !mastery.IsWeak
                orderby mastery.LastPracticedAt, mastery.MasteryScore
                select new
                {
                    mastery.TopicId,
                    Topic = topic.Name,
                    mastery.DifficultyLevel
                })
                .Take(5)
                .ToListAsync(ct);

            foreach (var candidate in additional)
            {
                if (recommendations.Any(r => r.TopicId == candidate.TopicId))
                    continue;

                recommendations.Add(new AdaptiveRecommendation
                {
                    TopicId = candidate.TopicId,
                    Topic = candidate.Topic,
                    Difficulty = candidate.DifficultyLevel,
                    QuestionCount = 3,
                    Confidence = 0.5d,
                    Reason = "Keep this topic active to maintain long-term retention."
                });

                if (recommendations.Count >= 5)
                    break;
            }
        }

        await _cache.SetAsync(cacheKey, recommendations, RecommendationCacheTtl);
        return recommendations;
    }

    private async Task<List<ReviewItem>> GetDueReviewsAsync(string userId, CancellationToken ct)
    {
        ValidateUserId(userId);

        var nowUtc = DateTime.UtcNow;
        var dueRows = await (
            from review in _db.ReviewSchedules.AsNoTracking()
            join topic in _db.Topics.AsNoTracking()
                on review.TopicId equals topic.Id
            join mastery in _db.UserTopicMasteries.AsNoTracking()
                    .Where(m => m.UserId == userId)
                on review.TopicId equals mastery.TopicId into masteryJoin
            from mastery in masteryJoin.DefaultIfEmpty()
            where review.UserId == userId && review.DueAt <= nowUtc
            orderby review.DueAt
            select new
            {
                review.QuestionId,
                review.TopicId,
                Topic = topic.Name,
                review.DueAt,
                review.IntervalDays,
                review.RepetitionCount,
                review.EasinessFactor,
                Difficulty = mastery != null ? mastery.DifficultyLevel : AdaptiveDifficultyLevels.Medium
            })
            .Take(100)
            .ToListAsync(ct);

        return dueRows
            .Select(x => new ReviewItem
            {
                QuestionId = x.QuestionId,
                TopicId = x.TopicId,
                Topic = x.Topic,
                DueAt = x.DueAt,
                IntervalDays = x.IntervalDays,
                RepetitionCount = x.RepetitionCount,
                EasinessFactor = x.EasinessFactor,
                Difficulty = x.Difficulty,
                Overdue = x.DueAt < nowUtc
            })
            .ToList();
    }

    private async Task DetectWeakTopicsAsync(string userId, CancellationToken ct)
    {
        ValidateUserId(userId);

        var nowUtc = DateTime.UtcNow;
        var aggregates = await BuildTopicPerformanceQuery(userId).ToListAsync(ct);
        if (aggregates.Count == 0)
            return;

        var topicIds = aggregates.Select(x => x.TopicId).Distinct().ToList();
        var existingMasteries = await _db.UserTopicMasteries
            .Where(m => m.UserId == userId && topicIds.Contains(m.TopicId))
            .ToDictionaryAsync(m => m.TopicId, ct);

        var hasChanges = false;

        foreach (var aggregate in aggregates)
        {
            if (!existingMasteries.TryGetValue(aggregate.TopicId, out var mastery))
            {
                mastery = new UserTopicMastery
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    TopicId = aggregate.TopicId,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
                _db.UserTopicMasteries.Add(mastery);
                existingMasteries[aggregate.TopicId] = mastery;
                hasChanges = true;
            }

            var wasWeak = mastery.IsWeak;
            ApplyAggregateToMastery(mastery, aggregate, nowUtc);

            if (mastery.IsWeak && !wasWeak)
            {
                _logger.LogInformation(
                    "Weak topic detected. UserId={UserId} TopicId={TopicId} Attempts={Attempts} Accuracy={Accuracy} Mastery={Mastery}",
                    userId,
                    aggregate.TopicId,
                    aggregate.Attempts,
                    aggregate.Accuracy,
                    mastery.MasteryScore);
            }

            hasChanges = true;
        }

        if (!hasChanges)
            return;

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(GetRecommendationCacheKey(userId));
    }

    private async Task<UserLearningProfile> EnsureLearningProfileAsync(string userId, DateTime nowUtc, CancellationToken ct)
    {
        var profile = await _db.UserLearningProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is not null)
            return profile;

        profile = new UserLearningProfile
        {
            UserId = userId,
            PreferredDifficulty = AdaptiveDifficultyLevels.Medium,
            RollingAccuracy = 0d,
            RollingAverageResponseSeconds = 0d,
            RollingWindowSize = 20,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };

        _db.UserLearningProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    private DifficultyPolicyHelper.DifficultyDecision UpdateLearningProfileAndDifficulty(
        UserLearningProfile profile,
        bool isCorrect,
        int responseTimeSeconds,
        DateTime nowUtc)
    {
        var attemptsBefore = Math.Max(0, profile.TotalAttempts);
        var alpha = 2d / (Math.Max(5, profile.RollingWindowSize) + 1d);

        var correctness = isCorrect ? 1d : 0d;
        profile.RollingAccuracy = attemptsBefore == 0
            ? correctness
            : (alpha * correctness) + ((1d - alpha) * profile.RollingAccuracy);

        profile.RollingAverageResponseSeconds = attemptsBefore == 0
            ? responseTimeSeconds
            : (alpha * responseTimeSeconds) + ((1d - alpha) * profile.RollingAverageResponseSeconds);

        profile.TotalAttempts++;
        if (isCorrect)
            profile.TotalCorrect++;

        profile.LastPracticeAt = nowUtc;
        profile.UpdatedAt = nowUtc;

        var decision = DifficultyPolicyHelper.Decide(
            profile.PreferredDifficulty,
            profile.RollingAccuracy,
            profile.RollingAverageResponseSeconds,
            profile.LastDifficultyChangeAt,
            nowUtc,
            profile.TotalAttempts);

        if (decision.Changed)
        {
            profile.PreferredDifficulty = decision.Difficulty;
            profile.LastDifficultyChangeAt = nowUtc;
        }

        return decision;
    }

    private async Task<ReviewSchedule> UpsertReviewScheduleAsync(
        string userId,
        int questionId,
        int topicId,
        bool isCorrect,
        double confidence,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var schedule = await _db.ReviewSchedules
            .FirstOrDefaultAsync(x => x.UserId == userId && x.QuestionId == questionId, ct);

        if (schedule is null)
        {
            schedule = new ReviewSchedule
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                QuestionId = questionId,
                TopicId = topicId,
                EasinessFactor = 2.5d,
                IntervalDays = 1,
                RepetitionCount = 0,
                DueAt = nowUtc.AddDays(1),
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };
            _db.ReviewSchedules.Add(schedule);
        }

        var result = Sm2SchedulerHelper.Compute(
            isCorrect,
            confidence,
            schedule.EasinessFactor,
            schedule.IntervalDays,
            schedule.RepetitionCount,
            nowUtc);

        schedule.EasinessFactor = result.EasinessFactor;
        schedule.IntervalDays = result.IntervalDays;
        schedule.RepetitionCount = result.RepetitionCount;
        schedule.DueAt = result.DueAt;
        schedule.LastReviewedAt = nowUtc;
        schedule.LastWasCorrect = isCorrect;
        schedule.UpdatedAt = nowUtc;

        _logger.LogDebug(
            "Review schedule updated. UserId={UserId} QuestionId={QuestionId} Correct={IsCorrect} EF={EF} IntervalDays={IntervalDays} RepetitionCount={RepetitionCount} DueAt={DueAt}",
            userId,
            questionId,
            isCorrect,
            schedule.EasinessFactor,
            schedule.IntervalDays,
            schedule.RepetitionCount,
            schedule.DueAt);

        return schedule;
    }

    // Bayesian Knowledge Tracing update for mastery probability
    private async Task<UserTopicMastery> RecalculateTopicMasteryAsync(
        string userId,
        int topicId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var aggregate = await BuildTopicPerformanceQuery(userId)
            .FirstOrDefaultAsync(x => x.TopicId == topicId, ct);

        if (aggregate is null)
        {
            return new UserTopicMastery
            {
                UserId = userId,
                TopicId = topicId
            };
        }

        var mastery = await _db.UserTopicMasteries
            .FirstOrDefaultAsync(x => x.UserId == userId && x.TopicId == topicId, ct);

        if (mastery is null)
        {
            mastery = new UserTopicMastery
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TopicId = topicId,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };
            _db.UserTopicMasteries.Add(mastery);
        }

        // BKT parameters (could be loaded from config/db per topic)
        double pL0 = mastery.MasteryScore > 0 ? mastery.MasteryScore / 100d : 0.2; // initial knowledge
        double pT = 0.15; // learning probability
        double pG = 0.2;  // guess probability
        double pS = 0.1;  // slip probability

        // Use last N attempts for BKT update
        var lastAttempts = await _db.UserQuestionHistories
            .Where(h => h.UserId == userId && h.TopicId == topicId)
            .OrderByDescending(h => h.AnsweredAt)
            .Take(10)
            .Select(h => h.IsCorrect)
            .ToListAsync(ct);

        double prior = pL0;
        foreach (var correct in lastAttempts)
        {
            double posterior = correct
                ? (prior * (1 - pS)) / (prior * (1 - pS) + (1 - prior) * pG)
                : (prior * pS) / (prior * pS + (1 - prior) * (1 - pG));
            prior = posterior + (1 - posterior) * pT;
        }
        var masteryProbability = Math.Round(prior, 3);
        mastery.MasteryScore = Math.Round(masteryProbability * 100d, 2);

        // Weakness score calculation
        double timePenalty = Math.Min(aggregate.Attempts > 0 ? aggregate.AverageConfidence : 0, 1.0);
        double weaknessScore = (1 - aggregate.Accuracy) * 0.5
            + (1 - masteryProbability) * 0.4
            + (1 - timePenalty) * 0.1;
        var roundedWeaknessScore = Math.Round(weaknessScore, 3);

        // Classification
        string weaknessLevel;
        if (aggregate.Attempts < WeakTopicAttemptThreshold)
            weaknessLevel = "insufficient_data";
        else if (weaknessScore < 0.3)
            weaknessLevel = "strong";
        else if (weaknessScore < 0.6)
            weaknessLevel = "medium";
        else
            weaknessLevel = "weak";

        mastery.Attempts = aggregate.Attempts;
        mastery.CorrectAttempts = aggregate.CorrectAttempts;
        mastery.AverageConfidence = Math.Round(aggregate.AverageConfidence, 3);
        mastery.LastPracticedAt = aggregate.LastAnsweredAt;
        mastery.IsWeak = string.Equals(weaknessLevel, "weak", StringComparison.OrdinalIgnoreCase);
        mastery.WeakDetectedAt = mastery.IsWeak ? nowUtc : null;
        mastery.UpdatedAt = nowUtc;

        _logger.LogDebug(
            "Mastery updated. UserId={UserId} TopicId={TopicId} Attempts={Attempts} Accuracy={Accuracy} MasteryProb={MasteryProbability} WeaknessScore={WeaknessScore} Level={WeaknessLevel}",
            userId,
            topicId,
            mastery.Attempts,
            aggregate.Accuracy,
            masteryProbability,
            roundedWeaknessScore,
            weaknessLevel);

        return mastery;
    }

    private void ApplyAggregateToMastery(UserTopicMastery mastery, TopicAggregate aggregate, DateTime nowUtc)
    {
        var previousScore = mastery.MasteryScore;
        mastery.Attempts = aggregate.Attempts;
        mastery.CorrectAttempts = aggregate.CorrectAttempts;
        mastery.AverageConfidence = Math.Round(aggregate.AverageConfidence, 3);
        mastery.LastPracticedAt = aggregate.LastAnsweredAt;

        mastery.MasteryScore = MasteryScoringHelper.CalculateMasteryScore(
            aggregate.Attempts,
            aggregate.CorrectAttempts,
            aggregate.AverageConfidence,
            aggregate.LastAnsweredAt,
            previousScore,
            nowUtc);

        var isWeakByAccuracy = aggregate.Attempts >= WeakTopicAttemptThreshold &&
                               aggregate.Accuracy < WeakTopicAccuracyThreshold;
        var isWeakByMastery = aggregate.Attempts >= WeakTopicAttemptThreshold &&
                              mastery.MasteryScore < 45d;

        mastery.IsWeak = isWeakByAccuracy || isWeakByMastery;
        mastery.WeakDetectedAt = mastery.IsWeak ? nowUtc : null;
        mastery.UpdatedAt = nowUtc;
    }

    private async Task TryUpdateLegacySrsAsync(string userId, int questionId, bool isCorrect, int responseTimeSeconds)
    {
        try
        {
            await _srsService.UpdateAsync(userId, new SrsUpdateDto
            {
                QuestionId = questionId,
                IsCorrect = isCorrect,
                TimeMs = Math.Max(0, responseTimeSeconds) * 1000
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Legacy SRS sync failed. UserId={UserId} QuestionId={QuestionId}",
                userId,
                questionId);
        }
    }

    private async Task<List<QuestionCandidate>> GetReviewCandidatesAsync(
        string userId,
        DateTime nowUtc,
        HashSet<int> usedQuestionIds,
        CancellationToken ct)
    {
        var excluded = usedQuestionIds.ToList();

        var rows = await (
            from review in _db.ReviewSchedules.AsNoTracking()
            join question in _db.Questions.AsNoTracking()
                on review.QuestionId equals question.Id
            join subtopic in _db.Subtopics.AsNoTracking()
                on question.SubtopicId equals subtopic.Id
            where review.UserId == userId &&
                  review.DueAt <= nowUtc &&
                  !excluded.Contains(question.Id)
            orderby review.DueAt
            select new
            {
                QuestionId = question.Id,
                TopicId = subtopic.TopicId,
                question.SubtopicId,
                question.Difficulty,
                review.DueAt
            })
            .Take(ReviewQuestionCount * 4)
            .ToListAsync(ct);

        return rows
            .Select(x => new QuestionCandidate(
                x.QuestionId,
                x.TopicId,
                x.SubtopicId,
                AdaptiveDifficultyMapper.FromQuestionDifficulty(x.Difficulty),
                "review",
                (nowUtc - x.DueAt).TotalMinutes))
            .ToList();
    }

    private async Task<List<QuestionCandidate>> GetTopicCandidatesAsync(
        string userId,
        IReadOnlyCollection<int> topicIds,
        string preferredDifficulty,
        string source,
        HashSet<int> usedQuestionIds,
        int limit,
        CancellationToken ct)
    {
        if (topicIds.Count == 0 || limit <= 0)
            return [];

        var distinctTopicIds = topicIds.Distinct().ToList();
        var excluded = usedQuestionIds.ToList();

        var rawCandidates = await (
            from question in _db.Questions.AsNoTracking()
            join subtopic in _db.Subtopics.AsNoTracking()
                on question.SubtopicId equals subtopic.Id
            where distinctTopicIds.Contains(subtopic.TopicId) &&
                  !excluded.Contains(question.Id)
            select new
            {
                QuestionId = question.Id,
                TopicId = subtopic.TopicId,
                question.SubtopicId,
                question.Difficulty
            })
            .Take(limit * 10)
            .ToListAsync(ct);

        if (rawCandidates.Count == 0)
            return [];

        var questionIds = rawCandidates.Select(x => x.QuestionId).Distinct().ToList();
        var questionHistory = await (
            from history in _db.UserQuestionHistories.AsNoTracking()
            where history.UserId == userId && questionIds.Contains(history.QuestionId)
            group history by history.QuestionId into g
            select new
            {
                QuestionId = g.Key,
                Attempts = g.Count(),
                LastAnsweredAt = g.Max(x => x.AnsweredAt)
            })
            .ToDictionaryAsync(x => x.QuestionId, ct);

        return rawCandidates
            .OrderBy(x => questionHistory.TryGetValue(x.QuestionId, out var h) ? h.Attempts : 0)
            .ThenBy(x => AdaptiveDifficultyMapper.DistanceFrom(preferredDifficulty, x.Difficulty))
            .ThenBy(x => questionHistory.TryGetValue(x.QuestionId, out var h) ? h.LastAnsweredAt : DateTime.MinValue)
            .Take(limit)
            .Select(x => new QuestionCandidate(
                x.QuestionId,
                x.TopicId,
                x.SubtopicId,
                AdaptiveDifficultyMapper.FromQuestionDifficulty(x.Difficulty),
                source,
                0d))
            .ToList();
    }

    private async Task<List<QuestionCandidate>> GetFallbackCandidatesAsync(
        string userId,
        string preferredDifficulty,
        HashSet<int> usedQuestionIds,
        int needed,
        CancellationToken ct)
    {
        if (needed <= 0)
            return [];

        var excluded = usedQuestionIds.ToList();

        var fallback = await (
            from question in _db.Questions.AsNoTracking()
            join subtopic in _db.Subtopics.AsNoTracking()
                on question.SubtopicId equals subtopic.Id
            where !excluded.Contains(question.Id)
            orderby AdaptiveDifficultyMapper.DistanceFrom(preferredDifficulty, question.Difficulty), question.Id
            select new QuestionCandidate(
                question.Id,
                subtopic.TopicId,
                question.SubtopicId,
                AdaptiveDifficultyMapper.FromQuestionDifficulty(question.Difficulty),
                "recent",
                0d))
            .Take(needed * 4)
            .ToListAsync(ct);

        return fallback;
    }

    private static void SelectCandidates(
        List<QuestionCandidate> destination,
        IEnumerable<QuestionCandidate> source,
        int needed,
        HashSet<int> usedQuestionIds)
    {
        if (needed <= 0)
            return;

        foreach (var candidate in source)
        {
            if (destination.Count >= SessionQuestionCount || needed <= 0)
                break;

            if (!usedQuestionIds.Add(candidate.QuestionId))
                continue;

            destination.Add(candidate);
            needed--;
        }
    }

    private static List<QuestionCandidate> BuildDifficultyAwareSequence(List<QuestionCandidate> source)
    {
        var working = source
            .OrderByDescending(x => x.Source == "review")
            .ThenByDescending(x => x.Source == "weak")
            .ThenByDescending(x => x.Priority)
            .ToList();

        var result = new List<QuestionCandidate>(working.Count);
        while (working.Count > 0)
        {
            var hardStreak = CountTrailingHard(result);
            var next = working.FirstOrDefault(x =>
                hardStreak < HardSequenceLimit ||
                !string.Equals(x.DifficultyLevel, AdaptiveDifficultyLevels.Hard, StringComparison.OrdinalIgnoreCase));

            if (next is null)
                next = working[0];

            result.Add(next);
            working.Remove(next);
        }

        return result;
    }

    private static int CountTrailingHard(List<QuestionCandidate> sequence)
    {
        var streak = 0;
        for (var i = sequence.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(sequence[i].DifficultyLevel, AdaptiveDifficultyLevels.Hard, StringComparison.OrdinalIgnoreCase))
                break;

            streak++;
        }

        return streak;
    }

    private static bool EvaluateAnswer(Question question, string answer)
    {
        var normalized = (answer ?? string.Empty).Trim();
        if (normalized.Length == 0)
            return false;

        if (question.Type == "multiple_choice")
        {
            var parsedId = int.TryParse(normalized, out var optionId) ? optionId : -1;
            return question.Options.Any(o =>
                o.IsCorrect &&
                (o.Id == parsedId || string.Equals(o.Text.Trim(), normalized, StringComparison.OrdinalIgnoreCase)));
        }

        return question.CorrectAnswer is not null &&
               string.Equals(question.CorrectAnswer.Trim(), normalized, StringComparison.OrdinalIgnoreCase);
    }

    // Example EF query pattern used by multiple methods:
    // aggregate user history to topic-level metrics directly in SQL.
    private IQueryable<TopicAggregate> BuildTopicPerformanceQuery(string userId)
    {
        return
            from history in _db.UserQuestionHistories.AsNoTracking()
            where history.UserId == userId
            group history by history.TopicId
            into g
            select new TopicAggregate(
                g.Key,
                g.Count(),
                g.Count(x => x.IsCorrect),
                g.Average(x => x.Confidence),
                g.Max(x => x.AnsweredAt));
    }

    private IQueryable<RecentTopicAggregate> BuildRecentTopicQuery(string userId)
    {
        return
            from history in _db.UserQuestionHistories.AsNoTracking()
            where history.UserId == userId
            group history by history.TopicId
            into g
            orderby g.Max(x => x.AnsweredAt) descending
            select new RecentTopicAggregate(
                g.Key,
                g.Max(x => x.AnsweredAt),
                g.Count());
    }

    private static void ValidateUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
    }

    private static void ValidateAnswerRequest(AdaptiveAnswerRequest request)
    {
        if (request.QuestionId <= 0)
            throw new ArgumentException("QuestionId must be a positive integer.", nameof(request.QuestionId));

        if (request.AdaptiveSessionId == Guid.Empty)
            throw new ArgumentException("AdaptiveSessionId is required.", nameof(request.AdaptiveSessionId));

        if (request.AdaptiveSessionItemId == Guid.Empty)
            throw new ArgumentException("AdaptiveSessionItemId is required.", nameof(request.AdaptiveSessionItemId));

        if (string.IsNullOrWhiteSpace(request.Answer))
            throw new ArgumentException("Answer is required.", nameof(request.Answer));
    }

    private static string GetRecommendationCacheKey(string userId) =>
        $"adaptive:recommendations:{userId}";

    private sealed record QuestionCandidate(
        int QuestionId,
        int TopicId,
        int SubtopicId,
        string DifficultyLevel,
        string Source,
        double Priority);

    private sealed record TopicAggregate(
        int TopicId,
        int Attempts,
        int CorrectAttempts,
        double AverageConfidence,
        DateTime LastAnsweredAt)
    {
        public double Accuracy => Attempts <= 0 ? 0d : (double)CorrectAttempts / Attempts;
    }

    private sealed record RecentTopicAggregate(
        int TopicId,
        DateTime LastAnsweredAt,
        int Attempts);
}
