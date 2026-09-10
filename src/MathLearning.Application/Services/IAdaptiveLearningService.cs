using MathLearning.Domain.Entities;
using MathLearning.Application.DTOs.Adaptive;

namespace MathLearning.Application.Services;

public interface IAdaptiveLearningService
{
    Task<AdaptiveSession> GeneratePracticeSessionAsync(string userId);

    Task<AdaptiveAnswerSubmissionResult> SubmitAnswerAsync(
        string userId,
        AdaptiveAnswerRequest request,
        CancellationToken cancellationToken = default);

    Task<List<AdaptiveRecommendation>> GetRecommendationsAsync(string userId);

    Task<List<ReviewItem>> GetDueReviewsAsync(string userId);

    Task<LearningMapDto> GetLearningMapAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasteryDto>> GetMasteryAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task DetectWeakTopicsAsync(string userId);
}
