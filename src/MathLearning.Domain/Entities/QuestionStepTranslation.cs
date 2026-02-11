namespace MathLearning.Domain.Entities;

public class QuestionStepTranslation
{
    public int Id { get; private set; }
    public int QuestionStepId { get; private set; }
    public string Lang { get; private set; } = "en";
    public string Text { get; private set; } = "";
    public string? Hint { get; private set; }

    public QuestionStep? QuestionStep { get; private set; }

    private QuestionStepTranslation() { }

    public QuestionStepTranslation(int questionStepId, string lang, string text, string? hint = null)
    {
        QuestionStepId = questionStepId;
        SetLang(lang);
        SetText(text);
        Hint = hint;
    }

    public void SetLang(string lang)
    {
        Lang = string.IsNullOrWhiteSpace(lang)
            ? throw new ArgumentException("Lang is required")
            : lang.Trim().ToLowerInvariant();
    }

    public void SetText(string text)
    {
        Text = string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("Step translation text is required")
            : text;
    }

    public void SetHint(string? hint) => Hint = hint;
}
