using MathLearning.Domain.Enums;

namespace MathLearning.Admin.Models;

public sealed class QuestionEditorModel
{
    public string Type { get; set; } = "multiple_choice";
    public string Text { get; set; } = string.Empty;
    public ContentFormat TextFormat { get; set; } = ContentFormat.MarkdownWithMath;
    public RenderMode TextRenderMode { get; set; } = RenderMode.Auto;
    public string? SemanticsAltText { get; set; }
    public string? Explanation { get; set; }
    public ContentFormat ExplanationFormat { get; set; } = ContentFormat.MarkdownWithMath;
    public RenderMode ExplanationRenderMode { get; set; } = RenderMode.Auto;
    public string? HintFormula { get; set; }
    public string? HintClue { get; set; }
    public string? HintFull { get; set; }
    public ContentFormat HintFormat { get; set; } = ContentFormat.MarkdownWithMath;
    public RenderMode HintRenderMode { get; set; } = RenderMode.Auto;
    public string? CorrectAnswer { get; set; }
    public int CategoryId { get; set; }
    public int SubtopicId { get; set; }
    public int Difficulty { get; set; } = 1;
    public List<QuestionOptionEditorModel> Options { get; set; } = CreateDefaultOptions();
    public List<QuestionStepEditorModel> Steps { get; set; } = [];

    public static List<QuestionOptionEditorModel> CreateDefaultOptions()
        => [new() { IsCorrect = true }, new()];
}

public sealed class QuestionOptionEditorModel
{
    public int? Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public ContentFormat TextFormat { get; set; } = ContentFormat.MarkdownWithMath;
    public RenderMode RenderMode { get; set; } = RenderMode.Auto;
    public string? SemanticsAltText { get; set; }
    public bool IsCorrect { get; set; }
}

public sealed class QuestionStepEditorModel
{
    public int? Id { get; set; }
    public int Order { get; set; }
    public string Text { get; set; } = string.Empty;
    public ContentFormat TextFormat { get; set; } = ContentFormat.MarkdownWithMath;
    public RenderMode TextRenderMode { get; set; } = RenderMode.Auto;
    public string? Hint { get; set; }
    public ContentFormat HintFormat { get; set; } = ContentFormat.MarkdownWithMath;
    public RenderMode HintRenderMode { get; set; } = RenderMode.Auto;
    public string? SemanticsAltText { get; set; }
    public bool Highlight { get; set; }
}
