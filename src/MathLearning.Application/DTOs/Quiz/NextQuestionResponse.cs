using MathLearning.Domain.Enums;

namespace MathLearning.Application.DTOs.Quiz;

public record NextQuestionResponse(
    int Id,
    string Type,
    string Text,
    List<OptionDto>? Options,
    int Difficulty,
    string? HintLight,
    string? HintMedium,
    string? HintFull,
    string? Explanation,
    List<StepExplanationDto>? Steps,
    ContentFormat TextFormat = ContentFormat.MarkdownWithMath,
    ContentFormat ExplanationFormat = ContentFormat.MarkdownWithMath,
    ContentFormat HintFormat = ContentFormat.MarkdownWithMath,
    RenderMode TextRenderMode = RenderMode.Auto,
    RenderMode ExplanationRenderMode = RenderMode.Auto,
    RenderMode HintRenderMode = RenderMode.Auto,
    string? SemanticsAltText = null
);
