using MathLearning.Domain.Explanations;

namespace MathLearning.Application.DTOs.Explanations;

public sealed record GenerateExplanationRequest(
    int? ProblemId,
    string? ProblemText,
    string? StudentAnswer,
    string? ExpectedAnswer,
    string? Topic,
    string? Subtopic,
    int Grade,
    string Difficulty,
    string Language,
    bool EnableAiTutorEnhancement = true,
    bool ForceRefresh = false);

public sealed record MistakeAnalysisRequest(
    int? ProblemId,
    string? ProblemText,
    string StudentAnswer,
    string? ExpectedAnswer,
    string? Topic,
    string? Subtopic,
    int Grade,
    string Difficulty,
    string Language,
    bool ForceRefresh = false);

public sealed record HintDto(
    string Text,
    string HintType,
    int RevealOrder);

public sealed record MathContextDto(
    string Topic,
    string Subtopic,
    int Grade,
    string Difficulty,
    string CommonMistakeType);

public sealed record StepExplanationItemDto(
    Guid Id,
    int Order,
    string Text,
    string StepType,
    string ExplanationType,
    bool Highlight,
    HintDto? Hint,
    string? LatexExpression,
    string? MathMlExpression,
    string? FormulaReferenceId,
    string Difficulty,
    MathContextDto Context,
    string? ImageUrl);

public sealed record FormulaReferenceDto(
    string Id,
    string Name,
    string Latex,
    string MathMl,
    string Description);

public sealed record MistakeInsightDto(
    string MistakeType,
    string Description,
    string Remediation,
    decimal Confidence,
    string? FormulaReferenceId);

public sealed record ExplanationResponseDto(
    int? ProblemId,
    string ProblemText,
    string ProblemHash,
    string Language,
    bool ServedFromCache,
    IReadOnlyList<StepExplanationItemDto> Steps,
    IReadOnlyList<FormulaReferenceDto> FormulaReferences,
    IReadOnlyList<MistakeInsightDto> Mistakes);

public sealed record MistakeAnalysisResponseDto(
    int? ProblemId,
    string ProblemText,
    string ProblemHash,
    bool ServedFromCache,
    IReadOnlyList<MistakeInsightDto> Mistakes,
    IReadOnlyList<StepExplanationItemDto> Steps,
    IReadOnlyList<FormulaReferenceDto> FormulaReferences);

public sealed record MathProblemDescriptor(
    int? ProblemId,
    string ProblemText,
    string? ExpectedAnswer,
    string? StudentAnswer,
    MathContext Context,
    string Language);
