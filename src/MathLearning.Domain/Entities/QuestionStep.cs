using MathLearning.Domain.Enums;

namespace MathLearning.Domain.Entities;

public class QuestionStep
{
    public int Id { get; private set; }
    public int QuestionId { get; private set; }
    public int StepIndex { get; private set; }
    public string Text { get; private set; } = "";
    public ContentFormat TextFormat { get; private set; } = ContentFormat.MarkdownWithMath;
    public ContentFormat HintFormat { get; private set; } = ContentFormat.MarkdownWithMath;
    public RenderMode TextRenderMode { get; private set; } = RenderMode.Auto;
    public RenderMode HintRenderMode { get; private set; } = RenderMode.Auto;
    public string? SemanticsAltText { get; private set; }
    public string? Hint { get; private set; }
    public bool Highlight { get; private set; }

    public Question? Question { get; private set; }
    public List<QuestionStepTranslation> Translations { get; private set; } = new();

    private QuestionStep() { }

    public QuestionStep(int questionId, int stepIndex, string text, string? hint = null, bool highlight = false)
    {
        QuestionId = questionId;
        StepIndex = stepIndex;
        SetText(text);
        Hint = hint;
        Highlight = highlight;
    }

    public QuestionStep(
        int questionId,
        int stepIndex,
        string text,
        string? hint,
        bool highlight,
        ContentFormat textFormat,
        ContentFormat hintFormat,
        RenderMode textRenderMode = RenderMode.Auto,
        RenderMode hintRenderMode = RenderMode.Auto,
        string? semanticsAltText = null)
        : this(questionId, stepIndex, text, hint, highlight)
    {
        TextFormat = textFormat;
        HintFormat = hintFormat;
        TextRenderMode = textRenderMode;
        HintRenderMode = hintRenderMode;
        SemanticsAltText = semanticsAltText;
    }

    public void SetText(string text)
    {
        Text = string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("Step text is required")
            : text;
    }

    public void SetHint(string? hint) => Hint = hint;
    public void SetHighlight(bool highlight) => Highlight = highlight;
    public void SetStepIndex(int index) => StepIndex = index;
    public void SetTextFormat(ContentFormat format) => TextFormat = format;
    public void SetHintFormat(ContentFormat format) => HintFormat = format;
    public void SetTextRenderMode(RenderMode renderMode) => TextRenderMode = renderMode;
    public void SetHintRenderMode(RenderMode renderMode) => HintRenderMode = renderMode;
    public void SetSemanticsAltText(string? semanticsAltText) => SemanticsAltText = semanticsAltText;
}
