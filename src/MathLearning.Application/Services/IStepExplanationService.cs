using MathLearning.Application.DTOs.Explanations;

namespace MathLearning.Application.Services;

public interface IStepExplanationService
{
    Task<ExplanationResponseDto> GetForProblemAsync(int problemId, string language, CancellationToken ct = default);

    Task<ExplanationResponseDto> GenerateAsync(GenerateExplanationRequest request, CancellationToken ct = default);

    Task<MistakeAnalysisResponseDto> AnalyzeMistakeAsync(MistakeAnalysisRequest request, CancellationToken ct = default);
}
