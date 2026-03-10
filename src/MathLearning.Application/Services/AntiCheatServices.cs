using MathLearning.Application.DTOs.AntiCheat;

namespace MathLearning.Application.Services;

public interface IAnswerPatternAntiCheatService
{
    Task<AntiCheatDetectionResultDto> EvaluateAndTrackAsync(
        AntiCheatAnswerObservationInput input,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AntiCheatDetectionResultDto>> EvaluateAndTrackBatchAsync(
        IReadOnlyList<AntiCheatAnswerObservationInput> inputs,
        CancellationToken cancellationToken = default);
}

public interface IAntiCheatMlPromptBuilder
{
    AntiCheatMlPromptDto BuildPrompt(
        AntiCheatAnswerObservationInput input,
        AntiCheatDetectionResultDto result,
        IReadOnlyDictionary<string, object?> featureSnapshot);
}

public interface IAntiCheatMlReviewService
{
    Task<int> ProcessPendingReviewsAsync(int take, CancellationToken cancellationToken = default);

    Task<AntiCheatDetectionItemDto> ProcessReviewAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IAntiCheatAdminService
{
    Task<AntiCheatOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AntiCheatDetectionItemDto>> GetDetectionsAsync(
        int take,
        string? reviewStatus,
        string? severity,
        CancellationToken cancellationToken = default);

    Task<AntiCheatDetectionItemDto> ReviewDetectionAsync(
        Guid id,
        string reviewStatus,
        string? notes,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<AntiCheatDetectionItemDto> TriggerMlReviewAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<int> RunMlReviewSweepAsync(
        int take,
        CancellationToken cancellationToken = default);
}
