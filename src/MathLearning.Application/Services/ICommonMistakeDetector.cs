using MathLearning.Application.DTOs.Explanations;

namespace MathLearning.Application.Services;

public interface ICommonMistakeDetector
{
    Task<IReadOnlyList<MistakeInsightDto>> DetectAsync(MathProblemDescriptor descriptor, CancellationToken ct = default);
}
