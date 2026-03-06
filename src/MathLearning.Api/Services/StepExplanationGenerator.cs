using MathLearning.Application.DTOs.Explanations;
using MathLearning.Application.Services;
using MathLearning.Domain.Explanations;

namespace MathLearning.Api.Services;

public sealed class StepExplanationGenerator : IStepExplanationGenerator
{
    public IReadOnlyList<StepExplanation> Generate(
        MathReasoningGraph graph,
        IReadOnlyDictionary<string, FormulaReferenceDefinition> formulaReferences,
        IReadOnlyList<MistakeInsightDto> mistakes)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var steps = new List<StepExplanation>(graph.Nodes.Count + mistakes.Count);
        var order = 1;

        foreach (var node in graph.Nodes)
        {
            var stepType = MapStepType(node.RuleApplied);
            var formulaReferenceId = node.FormulaReferenceId is not null && formulaReferences.ContainsKey(node.FormulaReferenceId)
                ? node.FormulaReferenceId
                : null;

            steps.Add(new StepExplanation(
                order++,
                node.Narrative ?? node.Expression,
                stepType,
                ExplanationType.Normal,
                stepType is StepType.FinalResult or StepType.Formula,
                BuildHint(node),
                node.Context.Difficulty,
                node.Context,
                node.LatexExpression,
                node.MathMlExpression,
                formulaReferenceId));
        }

        foreach (var mistake in mistakes)
        {
            steps.Add(new StepExplanation(
                order++,
                mistake.Description,
                StepType.MistakeExplanation,
                ExplanationType.MistakeCorrection,
                true,
                new Hint(mistake.Remediation, HintType.Warning, 1),
                DifficultyLevel.Medium,
                graph.RootNode.Context,
                formulaReferenceId: mistake.FormulaReferenceId));
        }

        EnsureSequentialOrder(steps);
        return steps;
    }

    private static Hint? BuildHint(MathReasoningNode node)
    {
        return node.RuleApplied switch
        {
            ReasoningRule.AddFractions => new Hint("Keep the denominator fixed and transform only what is needed.", HintType.Formula, 1),
            ReasoningRule.AddNumerators => new Hint("Only numerators change when denominators already match.", HintType.NextStep, 2),
            ReasoningRule.SimplifyFraction => new Hint("Look for the greatest common divisor.", HintType.Strategy, 2),
            ReasoningRule.IsolateVariable => new Hint("Undo operations in the reverse order.", HintType.Strategy, 1),
            ReasoningRule.ApplyFormula => new Hint("Substitute each known coefficient carefully.", HintType.Formula, 1),
            ReasoningRule.Unknown => new Hint("Try to identify the operation or equation pattern first.", HintType.General, 1),
            _ => null
        };
    }

    private static StepType MapStepType(ReasoningRule rule) => rule switch
    {
        ReasoningRule.ParseProblem => StepType.Intro,
        ReasoningRule.ApplyFormula => StepType.Formula,
        ReasoningRule.AddFractions or ReasoningRule.DistributeMultiplication or ReasoningRule.IsolateVariable or ReasoningRule.NormalizeEquation => StepType.Transformation,
        ReasoningRule.AddNumerators or ReasoningRule.EvaluateArithmetic => StepType.Calculation,
        ReasoningRule.SimplifyFraction => StepType.Simplification,
        ReasoningRule.FinalizeResult => StepType.FinalResult,
        _ => StepType.Transformation
    };

    private static void EnsureSequentialOrder(IReadOnlyList<StepExplanation> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Order != i + 1)
                throw new InvalidOperationException("Explanation step order must be sequential.");
        }
    }
}
