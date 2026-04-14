using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

public sealed class PracticeAnalyticsUpdater : IPracticeAnalyticsUpdater
{
    private readonly ApiDbContext _db;
    private readonly IWeaknessAnalysisScheduler _weaknessScheduler;
    private readonly ILogger<PracticeAnalyticsUpdater> _logger;

    public PracticeAnalyticsUpdater(
        ApiDbContext db,
        IWeaknessAnalysisScheduler weaknessScheduler,
        ILogger<PracticeAnalyticsUpdater> logger)
    {
        _db = db;
        _weaknessScheduler = weaknessScheduler;
        _logger = logger;
    }

    public async Task UpdateAggregatesAsync(PracticeAttemptAnalyticsInput attempt, CancellationToken ct = default)
    {
        var analyticsUserId = UserIdGuidMapper.FromIdentityUserId(attempt.UserId);
        var nowUtc = DateTime.UtcNow;

        _db.QuizAttempts.Add(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            UserId = analyticsUserId,
            QuizId = attempt.SessionId,
            QuestionId = attempt.QuestionId,
            TopicId = attempt.TopicId,
            SubtopicId = attempt.SubtopicId,
            Correct = attempt.IsCorrect,
            TimeSpentMs = Math.Max(0, attempt.TimeSpentMs),
            CreatedAt = attempt.AttemptedAtUtc
        });

        var topicStat = await _db.UserTopicStats
            .FirstOrDefaultAsync(x => x.UserId == analyticsUserId && x.TopicId == attempt.TopicId, ct);
        if (topicStat is null)
        {
            topicStat = new UserTopicStat
            {
                UserId = analyticsUserId,
                TopicId = attempt.TopicId
            };
            _db.UserTopicStats.Add(topicStat);
        }

        topicStat.TotalQuestions += 1;
        topicStat.CorrectAnswers += attempt.IsCorrect ? 1 : 0;
        topicStat.Accuracy = WeaknessScoring.CalculateAccuracy(topicStat.CorrectAnswers, topicStat.TotalQuestions);
        topicStat.LastAttempt = topicStat.LastAttempt == default
            ? attempt.AttemptedAtUtc
            : (topicStat.LastAttempt > attempt.AttemptedAtUtc ? topicStat.LastAttempt : attempt.AttemptedAtUtc);
        topicStat.WeaknessScore = WeaknessScoring.CalculateWeaknessScore(
            topicStat.Accuracy,
            topicStat.TotalQuestions,
            WeaknessScoring.CalculateRecencyFactor(topicStat.LastAttempt, nowUtc));

        var subtopicStat = await _db.UserSubtopicStats
            .FirstOrDefaultAsync(x => x.UserId == analyticsUserId && x.SubtopicId == attempt.SubtopicId, ct);
        if (subtopicStat is null)
        {
            subtopicStat = new UserSubtopicStat
            {
                UserId = analyticsUserId,
                SubtopicId = attempt.SubtopicId
            };
            _db.UserSubtopicStats.Add(subtopicStat);
        }

        subtopicStat.TotalQuestions += 1;
        subtopicStat.CorrectAnswers += attempt.IsCorrect ? 1 : 0;
        subtopicStat.Accuracy = WeaknessScoring.CalculateAccuracy(subtopicStat.CorrectAnswers, subtopicStat.TotalQuestions);
        subtopicStat.LastAttempt = subtopicStat.LastAttempt == default
            ? attempt.AttemptedAtUtc
            : (subtopicStat.LastAttempt > attempt.AttemptedAtUtc ? subtopicStat.LastAttempt : attempt.AttemptedAtUtc);
        subtopicStat.WeaknessScore = WeaknessScoring.CalculateWeaknessScore(
            subtopicStat.Accuracy,
            subtopicStat.TotalQuestions,
            WeaknessScoring.CalculateRecencyFactor(subtopicStat.LastAttempt, nowUtc));
    }

    public async Task UpdateDailyActivityAsync(
        string userId,
        DateOnly day,
        bool completed,
        CancellationToken ct = default)
    {
        var row = await _db.UserDailyStats
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Day == day, ct);

        if (row is null)
        {
            row = new UserDailyStat
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Day = day,
                Completed = completed
            };
            _db.UserDailyStats.Add(row);
        }
        else
        {
            row.Completed = row.Completed || completed;
        }
    }

    public Task TriggerWeaknessRecomputeAsync(string userId, CancellationToken ct = default)
    {
        var analyticsUserId = UserIdGuidMapper.FromIdentityUserId(userId);
        _weaknessScheduler.Enqueue(analyticsUserId);

        _logger.LogInformation(
            "Weakness recompute queued from practice flow. UserId={UserId} AnalyticsUserId={AnalyticsUserId}",
            userId,
            analyticsUserId);

        return Task.CompletedTask;
    }
}
