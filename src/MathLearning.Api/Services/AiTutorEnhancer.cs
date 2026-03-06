using MathLearning.Application.DTOs.Explanations;
using MathLearning.Application.Services;
using MathLearning.Domain.Explanations;

namespace MathLearning.Api.Services;

public sealed class AiTutorEnhancer : IAiTutorEnhancer
{
    public Task<IReadOnlyList<StepExplanation>> EnhanceAsync(
        MathProblemDescriptor descriptor,
        IReadOnlyList<StepExplanation> steps,
        IReadOnlyList<MistakeInsightDto> mistakes,
        CancellationToken ct = default)
    {
        if (steps.Count == 0)
            return Task.FromResult(steps);

        var enhanced = steps.ToList();
        var finalStep = enhanced.LastOrDefault();
        var conceptText = BuildConceptText(descriptor, mistakes);

        if (!string.IsNullOrWhiteSpace(conceptText))
        {
            enhanced.Insert(Math.Max(1, enhanced.Count - 1), new StepExplanation(
                order: enhanced.Count,
                text: conceptText,
                stepType: StepType.Visualization,
                explanationType: ExplanationType.ConceptClarification,
                highlight: false,
                hint: new Hint(
                    ExplanationEngineSupport.IsSerbian(descriptor.Language)
                        ? "Pokušaj da prepoznaš isti obrazac u sledećem sličnom zadatku."
                        : "Try to spot the same pattern in the next similar problem.",
                    HintType.Concept,
                    3),
                difficulty: descriptor.Context.Difficulty,
                context: descriptor.Context));
        }

        var resequenced = enhanced
            .Select((step, index) => new StepExplanation(
                index + 1,
                step.Text,
                step.StepType,
                step.ExplanationType,
                step.Highlight || ReferenceEquals(step, finalStep),
                step.Hint,
                step.Difficulty,
                step.Context,
                step.LatexExpression,
                step.MathMlExpression,
                step.FormulaReferenceId,
                step.ImageUrl,
                step.Id))
            .ToList();

        return Task.FromResult<IReadOnlyList<StepExplanation>>(resequenced);
    }

    private static string? BuildConceptText(MathProblemDescriptor descriptor, IReadOnlyList<MistakeInsightDto> mistakes)
    {
        if (mistakes.Count > 0)
        {
            return ExplanationEngineSupport.IsSerbian(descriptor.Language)
                ? "Ključna ideja: ne preskači pravilo koje određuje strukturu zadatka. Prvo prepoznaj obrazac, pa tek onda računaj."
                : "Key idea: do not skip the rule that defines the structure of the problem. Identify the pattern first, then compute.";
        }

        var topicOrSubtopic = $"{descriptor.Context.Topic} {descriptor.Context.Subtopic}";

        return topicOrSubtopic.Contains("fraction", StringComparison.OrdinalIgnoreCase) ||
               topicOrSubtopic.Contains("razlom", StringComparison.OrdinalIgnoreCase)
            ? ExplanationEngineSupport.IsSerbian(descriptor.Language)
                ? "Vizuelno razmišljaj o razlomku kao o delovima iste celine. Kada je celina ista, menja se samo broj delova."
                : "Visualize a fraction as parts of the same whole. When the whole stays the same, only the number of parts changes."
            : topicOrSubtopic.Contains("equation", StringComparison.OrdinalIgnoreCase) ||
              topicOrSubtopic.Contains("jedna", StringComparison.OrdinalIgnoreCase)
                ? ExplanationEngineSupport.IsSerbian(descriptor.Language)
                    ? "Vizuelizuj jednačinu kao vagu: šta uradiš levoj strani, moraš da uradiš i desnoj."
                    : "Visualize the equation as a balance scale: whatever you do on the left, you must do on the right."
                : null;
    }
}
