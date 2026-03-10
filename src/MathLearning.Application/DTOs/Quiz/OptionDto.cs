using MathLearning.Domain.Enums;

namespace MathLearning.Application.DTOs.Quiz;

public record OptionDto(
    int Id,
    string Text,
    ContentFormat TextFormat = ContentFormat.MarkdownWithMath,
    RenderMode RenderMode = RenderMode.Auto,
    string? SemanticsAltText = null
);
