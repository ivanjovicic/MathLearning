using MathLearning.Domain.Entities;

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

    Task DetectWeakTopicsAsync(string userId);
}
