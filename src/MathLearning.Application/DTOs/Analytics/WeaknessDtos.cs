namespace MathLearning.Application.DTOs.Analytics;

using MathLearning.Domain.Entities;

public sealed record WeakTopicDto(
    int TopicId,
    string TopicName,
    decimal Accuracy,
    string WeaknessLevel,
    decimal Confidence,
    decimal WeaknessScore,
    DateTime LastAttempt,
    int TotalQuestions);

public sealed record WeakSubtopicDto(
    int SubtopicId,
    string SubtopicName,
    int TopicId,
    string TopicName,
    decimal Accuracy,
    string WeaknessLevel,
    decimal Confidence,
    decimal WeaknessScore,
    DateTime LastAttempt,
    int TotalQuestions);

public sealed record PracticeRecommendationDto(
    string Id,
    string Title,
    int TopicId,
    int? SubtopicId,
    string Reason,
    decimal Priority)
{
    public string TopicName { get; init; } = string.Empty;
    public string RecommendedDifficulty { get; init; } = AdaptiveDifficultyLevels.Medium;
}

public sealed record PracticeRecommendationsResponse(
    IReadOnlyList<PracticeRecommendationDto> Recommendations,
    int Page,
    int PageSize,
    int Returned);
