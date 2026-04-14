using MathLearning.Application.DTOs.Explanations;
using MathLearning.Domain.Explanations;

namespace MathLearning.Application.Services;

public interface IAiTutorEnhancer
{
    Task<IReadOnlyList<StepExplanation>> EnhanceAsync(
        MathProblemDescriptor descriptor,
        IReadOnlyList<StepExplanation> steps,
        IReadOnlyList<MistakeInsightDto> mistakes,
        CancellationToken ct = default);
}
