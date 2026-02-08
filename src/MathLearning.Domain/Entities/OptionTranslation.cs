namespace MathLearning.Domain.Entities;

public class OptionTranslation
{
    public int Id { get; private set; }
    public int OptionId { get; private set; }
    public string Lang { get; private set; } = "en";
    public string Text { get; private set; } = "";

    public QuestionOption? Option { get; private set; }

    private OptionTranslation() { }

    public OptionTranslation(int optionId, string lang, string text)
    {
        OptionId = optionId;
        Lang = string.IsNullOrWhiteSpace(lang)
            ? throw new ArgumentException("Lang is required")
            : lang.Trim().ToLowerInvariant();
        Text = string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("Translation text is required")
            : text;
    }

    public void SetText(string text)
    {
        Text = string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("Translation text is required")
            : text;
    }
}
