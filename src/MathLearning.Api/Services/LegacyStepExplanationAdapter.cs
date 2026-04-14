using MathLearning.Application.DTOs.Explanations;
using MathLearning.Application.DTOs.Quiz;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Domain.Explanations;

namespace MathLearning.Api.Services;

public sealed class LegacyStepExplanationAdapter
{
    private static readonly IReadOnlyDictionary<string, FormulaReferenceDefinition> EmptyFormulaReferences =
        new Dictionary<string, FormulaReferenceDefinition>(StringComparer.OrdinalIgnoreCase);

    private readonly IMathReasoningGraphEngine _graphEngine;
    private readonly IStepExplanationGenerator _generator;

    public LegacyStepExplanationAdapter(
        IMathReasoningGraphEngine graphEngine,
        IStepExplanationGenerator generator)
    {
        _graphEngine = graphEngine;
        _generator = generator;
    }

    public List<StepExplanationDto> GetSteps(Question question, string language)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (question.Steps.Count > 0)
            return StepEngine.GetSteps(question, language);

        var descriptor = new MathProblemDescriptor(
            question.Id,
            TranslationHelper.GetText(question, language),
            question.CorrectAnswer ?? question.Options.FirstOrDefault(x => x.IsCorrect)?.Text,
            null,
            new MathContext(
                question.Subtopic?.Topic?.Name ?? "General Math",
                question.Subtopic?.Name ?? "General",
                0,
                MapDifficulty(question.Difficulty)),
            language);

        var graph = _graphEngine.Build(descriptor);
        if (graph.RootNode.RuleApplied == ReasoningRule.Unknown)
            return StepEngine.GetSteps(question, language);

        var steps = _generator.Generate(graph, EmptyFormulaReferences, Array.Empty<MistakeInsightDto>());
        if (steps.Count == 0)
            return StepEngine.GetSteps(question, language);

        return steps
            .Select(step => new StepExplanationDto(
                step.Text,
                step.Hint?.Text,
                step.Highlight))
            .ToList();
    }

    private static DifficultyLevel MapDifficulty(int difficulty) => difficulty switch
    {
        <= 2 => DifficultyLevel.Easy,
        3 => DifficultyLevel.Medium,
        4 => DifficultyLevel.Hard,
        _ => DifficultyLevel.Advanced
    };
}
