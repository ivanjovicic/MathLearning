using MathLearning.Domain.Enums;

namespace MathLearning.Application.DTOs.Quiz;

public record QuizQuestionDto(
    int Id,
    string Type,
    string Text,
    List<OptionDto>? Options,
    int Difficulty,
    string? HintLight,
    string? HintMedium,
    ContentFormat TextFormat = ContentFormat.MarkdownWithMath,
    ContentFormat HintFormat = ContentFormat.MarkdownWithMath,
    RenderMode TextRenderMode = RenderMode.Auto,
    RenderMode HintRenderMode = RenderMode.Auto,
    string? SemanticsAltText = null
);
