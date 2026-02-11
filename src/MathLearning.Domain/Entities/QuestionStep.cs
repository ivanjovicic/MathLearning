namespace MathLearning.Domain.Entities;

public class QuestionStep
{
    public int Id { get; private set; }
    public int QuestionId { get; private set; }
    public int StepIndex { get; private set; }
    public string Text { get; private set; } = "";
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

    public void SetText(string text)
    {
        Text = string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("Step text is required")
            : text;
    }

    public void SetHint(string? hint) => Hint = hint;
    public void SetHighlight(bool highlight) => Highlight = highlight;
    public void SetStepIndex(int index) => StepIndex = index;
}
