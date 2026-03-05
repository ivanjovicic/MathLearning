using MathLearning.Domain.Entities;

namespace MathLearning.Application.DTOs.Adaptive;

public sealed record AdaptiveSessionItemDto(
    Guid AdaptiveSessionItemId,
    int QuestionId,
    int TopicId,
    int SubtopicId,
    string SourceType,
    string DifficultyLevel,
    int Sequence);

public sealed record AdaptiveSessionDto(
    Guid AdaptiveSessionId,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    string ProfileDifficulty,
    IReadOnlyList<AdaptiveSessionItemDto> Items);

public sealed record AdaptivePathPayload(
    IReadOnlyList<AdaptiveRecommendation> Recommendations,
    IReadOnlyList<ReviewItem> DueReviews,
    DateTime GeneratedAtUtc);

public sealed record AdaptivePathResponse(
    AdaptivePathPayload Payload,
    bool ServedFromCache,
    bool IsStale,
    string? FallbackReason,
    DateTime? CachedAtUtc);

public sealed record AdaptiveRecommendationsPayload(
    IReadOnlyList<AdaptiveRecommendation> Recommendations,
    DateTime GeneratedAtUtc);

public sealed record AdaptiveRecommendationsResponse(
    AdaptiveRecommendationsPayload Payload,
    bool ServedFromCache,
    bool IsStale,
    string? FallbackReason,
    DateTime? CachedAtUtc);
