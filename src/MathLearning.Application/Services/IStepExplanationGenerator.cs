using MathLearning.Application.DTOs.Explanations;
using MathLearning.Domain.Explanations;

namespace MathLearning.Application.Services;

public interface IStepExplanationGenerator
{
    IReadOnlyList<StepExplanation> Generate(
        MathReasoningGraph graph,
        IReadOnlyDictionary<string, FormulaReferenceDefinition> formulaReferences,
        IReadOnlyList<MistakeInsightDto> mistakes);
}
