using MathLearning.Application.DTOs.Explanations;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Domain.Explanations;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Api.Services;

public sealed class StepExplanationService : IStepExplanationService
{
    private readonly ApiDbContext _db;
    private readonly IMathReasoningGraphEngine _graphEngine;
    private readonly IStepExplanationGenerator _generator;
    private readonly ICommonMistakeDetector _mistakeDetector;
    private readonly IFormulaReferenceService _formulaReferenceService;
    private readonly IAiTutorEnhancer _aiTutorEnhancer;
    private readonly IExplanationCacheService _cache;
    private readonly ILogger<StepExplanationService> _logger;

    public StepExplanationService(
        ApiDbContext db,
        IMathReasoningGraphEngine graphEngine,
        IStepExplanationGenerator generator,
        ICommonMistakeDetector mistakeDetector,
        IFormulaReferenceService formulaReferenceService,
        IAiTutorEnhancer aiTutorEnhancer,
        IExplanationCacheService cache,
        ILogger<StepExplanationService> logger)
    {
        _db = db;
        _graphEngine = graphEngine;
        _generator = generator;
        _mistakeDetector = mistakeDetector;
        _formulaReferenceService = formulaReferenceService;
        _aiTutorEnhancer = aiTutorEnhancer;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ExplanationResponseDto> GetForProblemAsync(int problemId, string language, CancellationToken ct = default)
    {
        var question = await LoadQuestionAsync(problemId, ct)
            ?? throw new KeyNotFoundException($"Problem {problemId} was not found.");

        var request = new GenerateExplanationRequest(
            problemId,
            GetQuestionText(question, language),
            null,
            GetExpectedAnswer(question, language),
            question.Subtopic?.Topic?.Name,
            question.Subtopic?.Name,
            0,
            MapQuestionDifficulty(question.Difficulty).ToString(),
            language,
            true,
            false);

        return await GenerateCoreAsync(request, question, ct);
    }

    public Task<ExplanationResponseDto> GenerateAsync(GenerateExplanationRequest request, CancellationToken ct = default) =>
        GenerateCoreAsync(request, null, ct);

    public async Task<MistakeAnalysisResponseDto> AnalyzeMistakeAsync(MistakeAnalysisRequest request, CancellationToken ct = default)
    {
        Question? question = null;
        if (request.ProblemId.HasValue)
            question = await LoadQuestionAsync(request.ProblemId.Value, ct)
                ?? throw new KeyNotFoundException($"Problem {request.ProblemId.Value} was not found.");

        var descriptor = BuildDescriptor(
            question,
            request.ProblemId,
            request.ProblemText,
            request.ExpectedAnswer,
            request.StudentAnswer,
            request.Topic,
            request.Subtopic,
            request.Grade,
            request.Difficulty,
            request.Language);

        var problemHash = ComputeProblemHash(descriptor);
        if (!request.ForceRefresh)
        {
            var cached = await _cache.GetMistakeAnalysisAsync(problemHash, descriptor.Context.Grade, descriptor.Context.Difficulty.ToString(), descriptor.Language, ct);
            if (cached is not null)
                return cached;
        }

        var graph = _graphEngine.Build(descriptor);
        var normalizedDescriptor = EnsureExpectedAnswer(descriptor, graph);
        var mistakes = await _mistakeDetector.DetectAsync(normalizedDescriptor, ct);
        var formulaReferences = await ResolveFormulaReferencesAsync(graph, mistakes, ct);

        IReadOnlyList<StepExplanation> steps = _generator.Generate(graph, formulaReferences, mistakes);
        steps = await _aiTutorEnhancer.EnhanceAsync(normalizedDescriptor, steps, mistakes, ct);

        var response = new MistakeAnalysisResponseDto(
            normalizedDescriptor.ProblemId,
            normalizedDescriptor.ProblemText,
            problemHash,
            false,
            mistakes,
            steps.Select(MapStep).ToList(),
            formulaReferences.Values.Select(MapFormula).ToList());

        await _cache.SetMistakeAnalysisAsync(problemHash, normalizedDescriptor.Context.Grade, normalizedDescriptor.Context.Difficulty.ToString(), normalizedDescriptor.Language, response, ct);
        return response;
    }

    private async Task<ExplanationResponseDto> GenerateCoreAsync(GenerateExplanationRequest request, Question? loadedQuestion, CancellationToken ct)
    {
        var question = loadedQuestion;
        if (question is null && request.ProblemId.HasValue)
        {
            question = await LoadQuestionAsync(request.ProblemId.Value, ct)
                ?? throw new KeyNotFoundException($"Problem {request.ProblemId.Value} was not found.");
        }

        var descriptor = BuildDescriptor(
            question,
            request.ProblemId,
            request.ProblemText,
            request.ExpectedAnswer,
            request.StudentAnswer,
            request.Topic,
            request.Subtopic,
            request.Grade,
            request.Difficulty,
            request.Language);

        var problemHash = ComputeProblemHash(descriptor);
        if (!request.ForceRefresh)
        {
            var cached = await _cache.GetExplanationAsync(problemHash, descriptor.Context.Grade, descriptor.Context.Difficulty.ToString(), descriptor.Language, ct);
            if (cached is not null)
                return cached;
        }

        var graph = _graphEngine.Build(descriptor);
        var normalizedDescriptor = EnsureExpectedAnswer(descriptor, graph);
        var mistakes = await _mistakeDetector.DetectAsync(normalizedDescriptor, ct);
        var formulaReferences = await ResolveFormulaReferencesAsync(graph, mistakes, ct);

        IReadOnlyList<StepExplanation> steps;
        if (question is not null &&
            question.Steps.Count > 0 &&
            graph.RootNode.RuleApplied == ReasoningRule.Unknown)
        {
            steps = BuildFromStoredSteps(question, normalizedDescriptor);
        }
        else
        {
            steps = _generator.Generate(graph, formulaReferences, mistakes);
        }

        if (request.EnableAiTutorEnhancement)
            steps = await _aiTutorEnhancer.EnhanceAsync(normalizedDescriptor, steps, mistakes, ct);

        var response = new ExplanationResponseDto(
            normalizedDescriptor.ProblemId,
            normalizedDescriptor.ProblemText,
            problemHash,
            normalizedDescriptor.Language,
            false,
            steps.Select(MapStep).ToList(),
            formulaReferences.Values.Select(MapFormula).ToList(),
            mistakes);

        await _cache.SetExplanationAsync(problemHash, normalizedDescriptor.Context.Grade, normalizedDescriptor.Context.Difficulty.ToString(), normalizedDescriptor.Language, response, ct);
        _logger.LogInformation("Generated explanation for problem hash {ProblemHash}.", problemHash);
        return response;
    }

    private async Task<Question?> LoadQuestionAsync(int problemId, CancellationToken ct)
    {
        return await _db.Questions
            .AsNoTracking()
            .Include(q => q.Options).ThenInclude(o => o.Translations)
            .Include(q => q.Translations)
            .Include(q => q.Steps).ThenInclude(s => s.Translations)
            .Include(q => q.Subtopic)
                .ThenInclude(s => s!.Topic)
            .FirstOrDefaultAsync(q => q.Id == problemId, ct);
    }

    private static MathProblemDescriptor BuildDescriptor(
        Question? question,
        int? problemId,
        string? problemText,
        string? expectedAnswer,
        string? studentAnswer,
        string? topic,
        string? subtopic,
        int grade,
        string difficulty,
        string language)
    {
        var resolvedDifficulty = question is not null
            ? MapQuestionDifficulty(question.Difficulty)
            : ExplanationEngineSupport.ParseDifficulty(difficulty);

        var context = new MathContext(
            topic: topic ?? question?.Subtopic?.Topic?.Name ?? "General Math",
            subtopic: subtopic ?? question?.Subtopic?.Name ?? "General",
            grade: grade,
            difficulty: resolvedDifficulty,
            commonMistakeType: CommonMistakeType.None);

        var resolvedProblemText = question is not null
            ? GetQuestionText(question, language)
            : problemText?.Trim();

        if (string.IsNullOrWhiteSpace(resolvedProblemText))
            throw new InvalidOperationException("Problem text is required.");

        var resolvedExpectedAnswer = question is not null
            ? GetExpectedAnswer(question, language)
            : expectedAnswer?.Trim();

        return new MathProblemDescriptor(
            problemId ?? question?.Id,
            resolvedProblemText,
            resolvedExpectedAnswer,
            string.IsNullOrWhiteSpace(studentAnswer) ? null : studentAnswer.Trim(),
            context,
            string.IsNullOrWhiteSpace(language) ? "en" : language.Trim().ToLowerInvariant());
    }

    private static MathProblemDescriptor EnsureExpectedAnswer(MathProblemDescriptor descriptor, MathReasoningGraph graph)
    {
        if (!string.IsNullOrWhiteSpace(descriptor.ExpectedAnswer))
            return descriptor;

        var finalExpression = graph.Nodes.LastOrDefault()?.Expression;
        return string.IsNullOrWhiteSpace(finalExpression)
            ? descriptor
            : descriptor with { ExpectedAnswer = finalExpression };
    }

    private async Task<IReadOnlyDictionary<string, FormulaReferenceDefinition>> ResolveFormulaReferencesAsync(
        MathReasoningGraph graph,
        IReadOnlyList<MistakeInsightDto> mistakes,
        CancellationToken ct)
    {
        var formulaIds = graph.Nodes
            .Select(x => x.FormulaReferenceId)
            .Concat(mistakes.Select(x => x.FormulaReferenceId))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return await _formulaReferenceService.GetByIdsAsync(formulaIds, ct);
    }

    private static IReadOnlyList<StepExplanation> BuildFromStoredSteps(Question question, MathProblemDescriptor descriptor)
    {
        return question.Steps
            .OrderBy(x => x.StepIndex)
            .Select((step, index) => new StepExplanation(
                index + 1,
                step.Text,
                index == 0 ? StepType.Intro : StepType.Transformation,
                ExplanationType.Normal,
                step.Highlight,
                string.IsNullOrWhiteSpace(step.Hint) ? null : new Hint(step.Hint!, HintType.NextStep, 1),
                descriptor.Context.Difficulty,
                descriptor.Context,
                ExplanationEngineSupport.ToLatex(step.Text),
                ExplanationEngineSupport.ToMathMl(step.Text)))
            .ToList();
    }

    private static StepExplanationItemDto MapStep(StepExplanation step) =>
        new(
            step.Id,
            step.Order,
            step.Text,
            ExplanationEngineSupport.ToContractString(step.StepType),
            ExplanationEngineSupport.ToContractString(step.ExplanationType),
            step.Highlight,
            step.Hint is null ? null : new HintDto(
                step.Hint.Text,
                ExplanationEngineSupport.ToContractString(step.Hint.HintType),
                step.Hint.RevealOrder),
            step.LatexExpression,
            step.MathMlExpression,
            step.FormulaReferenceId,
            ExplanationEngineSupport.ToContractString(step.Difficulty),
            new MathContextDto(
                step.Context.Topic,
                step.Context.Subtopic,
                step.Context.Grade,
                ExplanationEngineSupport.ToContractString(step.Context.Difficulty),
                ExplanationEngineSupport.ToContractString(step.Context.CommonMistakeType)),
            step.ImageUrl);

    private static FormulaReferenceDto MapFormula(FormulaReferenceDefinition formula) =>
        new(formula.Id, formula.Name, formula.Latex, formula.MathMl, formula.Description);

    private static string ComputeProblemHash(MathProblemDescriptor descriptor) =>
        ExplanationEngineSupport.ComputeHash(
            descriptor.ProblemId?.ToString(),
            descriptor.ProblemText,
            descriptor.ExpectedAnswer,
            descriptor.StudentAnswer,
            descriptor.Context.Topic,
            descriptor.Context.Subtopic,
            descriptor.Context.Grade.ToString(),
            descriptor.Context.Difficulty.ToString(),
            descriptor.Language);

    private static string GetQuestionText(Question question, string language) =>
        TranslationHelper.GetText(question, language);

    private static string? GetExpectedAnswer(Question question, string language)
    {
        if (!string.IsNullOrWhiteSpace(question.CorrectAnswer))
            return question.CorrectAnswer;

        return question.Options.FirstOrDefault(o => o.IsCorrect) is { } correctOption
            ? TranslationHelper.GetOptionText(correctOption, language)
            : null;
    }

    private static DifficultyLevel MapQuestionDifficulty(int difficulty) => difficulty switch
    {
        <= 2 => DifficultyLevel.Easy,
        3 => DifficultyLevel.Medium,
        4 => DifficultyLevel.Hard,
        _ => DifficultyLevel.Advanced
    };
}
