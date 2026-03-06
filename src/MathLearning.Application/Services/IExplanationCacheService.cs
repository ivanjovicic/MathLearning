using MathLearning.Application.DTOs.Explanations;

namespace MathLearning.Application.Services;

public interface IExplanationCacheService
{
    Task<ExplanationResponseDto?> GetExplanationAsync(string problemHash, int grade, string difficulty, string language, CancellationToken ct = default);

    Task<MistakeAnalysisResponseDto?> GetMistakeAnalysisAsync(string problemHash, int grade, string difficulty, string language, CancellationToken ct = default);

    Task SetExplanationAsync(string problemHash, int grade, string difficulty, string language, ExplanationResponseDto response, CancellationToken ct = default);

    Task SetMistakeAnalysisAsync(string problemHash, int grade, string difficulty, string language, MistakeAnalysisResponseDto response, CancellationToken ct = default);
}
