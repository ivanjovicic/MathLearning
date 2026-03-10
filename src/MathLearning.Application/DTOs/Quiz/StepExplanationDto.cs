using MathLearning.Domain.Enums;

namespace MathLearning.Application.DTOs.Quiz;

public record StepExplanationDto(
    string Text,
    string? Hint,
    bool Highlight,
    ContentFormat TextFormat = ContentFormat.MarkdownWithMath,
    ContentFormat HintFormat = ContentFormat.MarkdownWithMath,
    RenderMode TextRenderMode = RenderMode.Auto,
    RenderMode HintRenderMode = RenderMode.Auto,
    string? SemanticsAltText = null
);
