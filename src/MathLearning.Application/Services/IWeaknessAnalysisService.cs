using MathLearning.Application.DTOs.Analytics;

namespace MathLearning.Application.Services;

public interface IWeaknessAnalysisService
{
    Task AnalyzeUserAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<WeakTopicDto>> GetWeakTopicsAsync(
        Guid userId,
        int take = 5,
        CancellationToken ct = default);

    Task<IReadOnlyList<WeakSubtopicDto>> GetWeakSubtopicsAsync(
        Guid userId,
        int take = 10,
        CancellationToken ct = default);

    Task<IReadOnlyList<PracticeRecommendationDto>> GeneratePracticeRecommendationsAsync(
        Guid userId,
        int take = 10,
        CancellationToken ct = default);
}
