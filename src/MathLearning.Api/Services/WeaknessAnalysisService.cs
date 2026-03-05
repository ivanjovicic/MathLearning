using System.Text.Json;
using MathLearning.Application.DTOs.Analytics;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

public sealed class WeaknessAnalysisService : IWeaknessAnalysisService
{
    private readonly ApiDbContext _db;
    private readonly ILogger<WeaknessAnalysisService> _logger;

    public WeaknessAnalysisService(ApiDbContext db, ILogger<WeaknessAnalysisService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AnalyzeUserAsync(Guid userId, CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        var topicStats = await _db.UserTopicStats
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
        var subtopicStats = await _db.UserSubtopicStats
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var existingWeakness = await _db.UserWeaknesses
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        if (topicStats.Count == 0 && subtopicStats.Count == 0)
        {
            if (existingWeakness.Count > 0)
            {
                _db.UserWeaknesses.RemoveRange(existingWeakness);
                await _db.SaveChangesAsync(ct);
            }
            return;
        }

        var attempts = await _db.QuizAttempts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.TopicId, x.SubtopicId, x.TimeSpentMs })
            .ToListAsync(ct);

        var userP95 = WeaknessScoring.Percentile95(attempts.Select(x => x.TimeSpentMs).ToArray());
        var topicP95 = attempts
            .GroupBy(x => x.TopicId)
            .ToDictionary(g => g.Key, g => WeaknessScoring.Percentile95(g.Select(x => x.TimeSpentMs).ToArray()));
        var subtopicP95 = attempts
            .GroupBy(x => x.SubtopicId)
            .ToDictionary(g => g.Key, g => WeaknessScoring.Percentile95(g.Select(x => x.TimeSpentMs).ToArray()));

        var topicNames = await _db.Topics
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var subtopicRows = await _db.Subtopics
            .AsNoTracking()
            .Select(x => new { x.Id, x.Name, x.TopicId })
            .ToListAsync(ct);
        var subtopicNameById = subtopicRows.ToDictionary(x => x.Id, x => x.Name);
        var subtopicTopicById = subtopicRows.ToDictionary(x => x.Id, x => x.TopicId);

        var existingByKey = existingWeakness.ToDictionary(
            x => BuildWeaknessKey(x.TopicId, x.SubtopicId),
            x => x);
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var stat in topicStats)
        {
            stat.Accuracy = WeaknessScoring.CalculateAccuracy(stat.CorrectAnswers, stat.TotalQuestions);

            var recencyFactor = WeaknessScoring.CalculateRecencyFactor(stat.LastAttempt, nowUtc);
            var weaknessScore = WeaknessScoring.CalculateWeaknessScore(stat.Accuracy, stat.TotalQuestions, recencyFactor);
            weaknessScore = WeaknessScoring.BoostWeaknessForSlowSolve(
                weaknessScore,
                stat.Accuracy,
                topicP95.GetValueOrDefault(stat.TopicId),
                userP95);

            stat.WeaknessScore = weaknessScore;
            var confidence = WeaknessScoring.CalculateConfidence(stat.TotalQuestions, recencyFactor);
            var level = WeaknessScoring.MapWeaknessLevel(stat.Accuracy);

            var topicName = topicNames.GetValueOrDefault(stat.TopicId, $"Topic {stat.TopicId}");
            UpsertWeakness(
                userId: userId,
                topicId: stat.TopicId,
                subtopicId: null,
                level: level,
                confidence: confidence,
                recommendedPractice: BuildPracticeJson(
                    id: $"topic_{stat.TopicId}_practice",
                    title: $"{topicName} - focused practice",
                    topicId: stat.TopicId,
                    subtopicId: null,
                    reason: BuildReason(stat.Accuracy, level),
                    priority: CalculatePriority(stat.WeaknessScore, confidence)),
                updatedAt: nowUtc,
                existingByKey: existingByKey,
                seenKeys: seenKeys);
        }

        foreach (var stat in subtopicStats)
        {
            stat.Accuracy = WeaknessScoring.CalculateAccuracy(stat.CorrectAnswers, stat.TotalQuestions);

            var recencyFactor = WeaknessScoring.CalculateRecencyFactor(stat.LastAttempt, nowUtc);
            var weaknessScore = WeaknessScoring.CalculateWeaknessScore(stat.Accuracy, stat.TotalQuestions, recencyFactor);
            weaknessScore = WeaknessScoring.BoostWeaknessForSlowSolve(
                weaknessScore,
                stat.Accuracy,
                subtopicP95.GetValueOrDefault(stat.SubtopicId),
                userP95);

            stat.WeaknessScore = weaknessScore;
            var confidence = WeaknessScoring.CalculateConfidence(stat.TotalQuestions, recencyFactor);
            var level = WeaknessScoring.MapWeaknessLevel(stat.Accuracy);

            var subtopicName = subtopicNameById.GetValueOrDefault(stat.SubtopicId, $"Subtopic {stat.SubtopicId}");
            var topicId = subtopicTopicById.GetValueOrDefault(stat.SubtopicId);
            UpsertWeakness(
                userId: userId,
                topicId: topicId <= 0 ? null : topicId,
                subtopicId: stat.SubtopicId,
                level: level,
                confidence: confidence,
                recommendedPractice: BuildPracticeJson(
                    id: $"subtopic_{stat.SubtopicId}_practice",
                    title: $"{subtopicName} - targeted drill",
                    topicId: topicId <= 0 ? null : topicId,
                    subtopicId: stat.SubtopicId,
                    reason: BuildReason(stat.Accuracy, level),
                    priority: CalculatePriority(stat.WeaknessScore, confidence)),
                updatedAt: nowUtc,
                existingByKey: existingByKey,
                seenKeys: seenKeys);
        }

        foreach (var stale in existingByKey.Values.Where(x => !seenKeys.Contains(BuildWeaknessKey(x.TopicId, x.SubtopicId))))
            _db.UserWeaknesses.Remove(stale);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Weakness analysis completed. UserId={UserId} TopicStats={TopicCount} SubtopicStats={SubtopicCount} WeaknessRows={WeaknessCount}",
            userId,
            topicStats.Count,
            subtopicStats.Count,
            seenKeys.Count);
    }

    public async Task<IReadOnlyList<WeakTopicDto>> GetWeakTopicsAsync(Guid userId, int take = 5, CancellationToken ct = default)
    {
        var count = Math.Clamp(take, 1, 100);
        var rows = await (
            from stat in _db.UserTopicStats.AsNoTracking()
            join topic in _db.Topics.AsNoTracking()
                on stat.TopicId equals topic.Id
            join weakness in _db.UserWeaknesses.AsNoTracking()
                .Where(x => x.UserId == userId && x.SubtopicId == null)
                on stat.TopicId equals weakness.TopicId into weaknessJoin
            from weakness in weaknessJoin.DefaultIfEmpty()
            where stat.UserId == userId && stat.TotalQuestions > 0
            orderby stat.WeaknessScore descending, stat.Accuracy ascending, stat.TotalQuestions descending
            select new WeakTopicDto(
                stat.TopicId,
                topic.Name,
                stat.Accuracy,
                weakness != null ? weakness.WeaknessLevel : WeaknessScoring.MapWeaknessLevel(stat.Accuracy),
                weakness != null
                    ? weakness.Confidence
                    : WeaknessScoring.CalculateConfidence(
                        stat.TotalQuestions,
                        WeaknessScoring.CalculateRecencyFactor(stat.LastAttempt, DateTime.UtcNow)),
                stat.WeaknessScore,
                stat.LastAttempt,
                stat.TotalQuestions))
            .Take(count)
            .ToListAsync(ct);

        return rows;
    }

    public async Task<IReadOnlyList<WeakSubtopicDto>> GetWeakSubtopicsAsync(Guid userId, int take = 10, CancellationToken ct = default)
    {
        var count = Math.Clamp(take, 1, 200);
        var rows = await (
            from stat in _db.UserSubtopicStats.AsNoTracking()
            join subtopic in _db.Subtopics.AsNoTracking()
                on stat.SubtopicId equals subtopic.Id
            join topic in _db.Topics.AsNoTracking()
                on subtopic.TopicId equals topic.Id
            join weakness in _db.UserWeaknesses.AsNoTracking()
                .Where(x => x.UserId == userId && x.SubtopicId != null)
                on stat.SubtopicId equals weakness.SubtopicId into weaknessJoin
            from weakness in weaknessJoin.DefaultIfEmpty()
            where stat.UserId == userId && stat.TotalQuestions > 0
            orderby stat.WeaknessScore descending, stat.Accuracy ascending, stat.TotalQuestions descending
            select new WeakSubtopicDto(
                stat.SubtopicId,
                subtopic.Name,
                topic.Id,
                topic.Name,
                stat.Accuracy,
                weakness != null ? weakness.WeaknessLevel : WeaknessScoring.MapWeaknessLevel(stat.Accuracy),
                weakness != null
                    ? weakness.Confidence
                    : WeaknessScoring.CalculateConfidence(
                        stat.TotalQuestions,
                        WeaknessScoring.CalculateRecencyFactor(stat.LastAttempt, DateTime.UtcNow)),
                stat.WeaknessScore,
                stat.LastAttempt,
                stat.TotalQuestions))
            .Take(count)
            .ToListAsync(ct);

        return rows;
    }

    public async Task<IReadOnlyList<PracticeRecommendationDto>> GeneratePracticeRecommendationsAsync(
        Guid userId,
        int take = 10,
        CancellationToken ct = default)
    {
        var count = Math.Clamp(take, 1, 100);
        var subtopics = await GetWeakSubtopicsAsync(userId, count * 2, ct);
        var topics = await GetWeakTopicsAsync(userId, count * 2, ct);

        var recommendations = new List<PracticeRecommendationDto>(subtopics.Count + topics.Count);

        foreach (var row in subtopics)
        {
            recommendations.Add(new PracticeRecommendationDto(
                Id: $"subtopic_{row.SubtopicId}_practice",
                Title: $"{row.SubtopicName} - targeted drill",
                TopicId: row.TopicId,
                SubtopicId: row.SubtopicId,
                Reason: BuildReason(row.Accuracy, row.WeaknessLevel),
                Priority: CalculatePriority(row.WeaknessScore, row.Confidence)));
        }

        foreach (var row in topics)
        {
            recommendations.Add(new PracticeRecommendationDto(
                Id: $"topic_{row.TopicId}_practice",
                Title: $"{row.TopicName} - focused practice",
                TopicId: row.TopicId,
                SubtopicId: null,
                Reason: BuildReason(row.Accuracy, row.WeaknessLevel),
                Priority: CalculatePriority(row.WeaknessScore, row.Confidence)));
        }

        var result = recommendations
            .OrderByDescending(x => x.Priority)
            .DistinctBy(x => x.Id)
            .Take(count)
            .ToList();

        return result;
    }

    private static decimal CalculatePriority(decimal weaknessScore, decimal confidence)
    {
        var normalizedWeakness = Math.Clamp(weaknessScore / 1.5m, 0m, 1m);
        var priority = (normalizedWeakness * 0.7m) + (confidence * 0.3m);
        return decimal.Round(Math.Clamp(priority, 0m, 1m), 4, MidpointRounding.AwayFromZero);
    }

    private static string BuildReason(decimal accuracy, string weaknessLevel)
    {
        if (weaknessLevel == WeaknessLevels.High || accuracy < 0.60m)
            return "low_accuracy";

        if (weaknessLevel == WeaknessLevels.Medium)
            return "borderline_accuracy";

        return "retention_maintenance";
    }

    private static string BuildPracticeJson(
        string id,
        string title,
        int? topicId,
        int? subtopicId,
        string reason,
        decimal priority)
    {
        return JsonSerializer.Serialize(new
        {
            id,
            title,
            topicId,
            subtopicId,
            reason,
            priority
        });
    }

    private void UpsertWeakness(
        Guid userId,
        int? topicId,
        int? subtopicId,
        string level,
        decimal confidence,
        string recommendedPractice,
        DateTime updatedAt,
        Dictionary<string, UserWeakness> existingByKey,
        HashSet<string> seenKeys)
    {
        var key = BuildWeaknessKey(topicId, subtopicId);
        seenKeys.Add(key);

        if (!existingByKey.TryGetValue(key, out var row))
        {
            row = new UserWeakness
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TopicId = topicId,
                SubtopicId = subtopicId
            };
            _db.UserWeaknesses.Add(row);
            existingByKey[key] = row;
        }

        row.WeaknessLevel = level;
        row.Confidence = confidence;
        row.RecommendedPractice = recommendedPractice;
        row.UpdatedAt = updatedAt;
    }

    private static string BuildWeaknessKey(int? topicId, int? subtopicId) =>
        $"{topicId?.ToString() ?? "-"}|{subtopicId?.ToString() ?? "-"}";
}
