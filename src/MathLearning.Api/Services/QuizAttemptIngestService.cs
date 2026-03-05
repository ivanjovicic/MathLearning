using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

public sealed class QuizAttemptIngestService : IQuizAttemptIngestService
{
    private readonly ApiDbContext _db;
    private readonly IWeaknessAnalysisScheduler _scheduler;
    private readonly ILogger<QuizAttemptIngestService> _logger;

    public QuizAttemptIngestService(
        ApiDbContext db,
        IWeaknessAnalysisScheduler scheduler,
        ILogger<QuizAttemptIngestService> logger)
    {
        _db = db;
        _scheduler = scheduler;
        _logger = logger;
    }

    public async Task IngestAttemptsAsync(
        string userId,
        IReadOnlyList<QuizAttemptIngestItem> attempts,
        CancellationToken ct = default)
    {
        if (attempts.Count == 0)
            return;

        var analyticsUserId = UserIdGuidMapper.FromIdentityUserId(userId);
        var subtopicIds = attempts.Select(x => x.SubtopicId).Distinct().ToList();
        var subtopicToTopic = await _db.Subtopics
            .AsNoTracking()
            .Where(x => subtopicIds.Contains(x.Id))
            .Select(x => new { x.Id, x.TopicId })
            .ToDictionaryAsync(x => x.Id, x => x.TopicId, ct);

        var nowUtc = DateTime.UtcNow;
        var topicDelta = new Dictionary<int, (int Total, int Correct, DateTime LastAttempt)>();
        var subtopicDelta = new Dictionary<int, (int Total, int Correct, DateTime LastAttempt)>();

        var useTransaction = _db.Database.IsRelational();
        await using var tx = useTransaction
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        foreach (var row in attempts)
        {
            if (!subtopicToTopic.TryGetValue(row.SubtopicId, out var topicId))
                continue;

            _db.QuizAttempts.Add(new QuizAttempt
            {
                Id = Guid.NewGuid(),
                UserId = analyticsUserId,
                QuizId = row.QuizId,
                QuestionId = row.QuestionId,
                TopicId = topicId,
                SubtopicId = row.SubtopicId,
                Correct = row.Correct,
                TimeSpentMs = Math.Max(0, row.TimeSpentMs),
                CreatedAt = row.CreatedAtUtc
            });

            topicDelta[topicId] = MergeDelta(topicDelta.GetValueOrDefault(topicId), row.Correct, row.CreatedAtUtc);
            subtopicDelta[row.SubtopicId] = MergeDelta(subtopicDelta.GetValueOrDefault(row.SubtopicId), row.Correct, row.CreatedAtUtc);
        }

        var topicIds = topicDelta.Keys.ToList();
        var subtopicIdsDelta = subtopicDelta.Keys.ToList();

        var topicStats = await _db.UserTopicStats
            .Where(x => x.UserId == analyticsUserId && topicIds.Contains(x.TopicId))
            .ToDictionaryAsync(x => x.TopicId, ct);
        var subtopicStats = await _db.UserSubtopicStats
            .Where(x => x.UserId == analyticsUserId && subtopicIdsDelta.Contains(x.SubtopicId))
            .ToDictionaryAsync(x => x.SubtopicId, ct);

        foreach (var (topicId, delta) in topicDelta)
        {
            if (!topicStats.TryGetValue(topicId, out var stat))
            {
                stat = new UserTopicStat
                {
                    UserId = analyticsUserId,
                    TopicId = topicId,
                    TotalQuestions = 0,
                    CorrectAnswers = 0,
                    Accuracy = 0m,
                    LastAttempt = delta.LastAttempt,
                    WeaknessScore = 0m
                };
                _db.UserTopicStats.Add(stat);
                topicStats[topicId] = stat;
            }

            stat.TotalQuestions += delta.Total;
            stat.CorrectAnswers += delta.Correct;
            stat.Accuracy = WeaknessScoring.CalculateAccuracy(stat.CorrectAnswers, stat.TotalQuestions);
            stat.LastAttempt = MaxUtc(stat.LastAttempt, delta.LastAttempt);
            stat.WeaknessScore = WeaknessScoring.CalculateWeaknessScore(
                stat.Accuracy,
                stat.TotalQuestions,
                WeaknessScoring.CalculateRecencyFactor(stat.LastAttempt, nowUtc));
        }

        foreach (var (subtopicId, delta) in subtopicDelta)
        {
            if (!subtopicStats.TryGetValue(subtopicId, out var stat))
            {
                stat = new UserSubtopicStat
                {
                    UserId = analyticsUserId,
                    SubtopicId = subtopicId,
                    TotalQuestions = 0,
                    CorrectAnswers = 0,
                    Accuracy = 0m,
                    LastAttempt = delta.LastAttempt,
                    WeaknessScore = 0m
                };
                _db.UserSubtopicStats.Add(stat);
                subtopicStats[subtopicId] = stat;
            }

            stat.TotalQuestions += delta.Total;
            stat.CorrectAnswers += delta.Correct;
            stat.Accuracy = WeaknessScoring.CalculateAccuracy(stat.CorrectAnswers, stat.TotalQuestions);
            stat.LastAttempt = MaxUtc(stat.LastAttempt, delta.LastAttempt);
            stat.WeaknessScore = WeaknessScoring.CalculateWeaknessScore(
                stat.Accuracy,
                stat.TotalQuestions,
                WeaknessScoring.CalculateRecencyFactor(stat.LastAttempt, nowUtc));
        }

        await _db.SaveChangesAsync(ct);
        if (tx is not null)
            await tx.CommitAsync(ct);

        _scheduler.Enqueue(analyticsUserId);

        _logger.LogInformation(
            "Quiz attempts ingested. UserId={UserId} AnalyticsUserId={AnalyticsUserId} Attempts={AttemptsCount} TopicStatsUpdated={TopicStats} SubtopicStatsUpdated={SubtopicStats}",
            userId,
            analyticsUserId,
            attempts.Count,
            topicDelta.Count,
            subtopicDelta.Count);
    }

    private static (int Total, int Correct, DateTime LastAttempt) MergeDelta(
        (int Total, int Correct, DateTime LastAttempt) current,
        bool correct,
        DateTime attemptAt)
    {
        var total = current.Total + 1;
        var correctCount = current.Correct + (correct ? 1 : 0);
        var last = current.LastAttempt == default ? attemptAt : MaxUtc(current.LastAttempt, attemptAt);
        return (total, correctCount, last);
    }

    private static DateTime MaxUtc(DateTime left, DateTime right) =>
        left >= right ? left : right;
}
