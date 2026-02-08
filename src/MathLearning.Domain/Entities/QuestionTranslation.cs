namespace MathLearning.Domain.Entities;

public class QuestionTranslation
{
    public int Id { get; private set; }
    public int QuestionId { get; private set; }
    public string Lang { get; private set; } = "en";
    public string Text { get; private set; } = "";
    public string? Explanation { get; private set; }
    public string? HintFormula { get; private set; }
    public string? HintClue { get; private set; }
    public string? HintLight { get; private set; }
    public string? HintMedium { get; private set; }
    public string? HintFull { get; private set; }

    public Question? Question { get; private set; }

    private QuestionTranslation() { }

    public QuestionTranslation(int questionId, string lang, string text,
        string? explanation = null, string? hintFormula = null, string? hintClue = null,
        string? hintLight = null, string? hintMedium = null, string? hintFull = null)
    {
        QuestionId = questionId;
        SetLang(lang);
        SetText(text);
        Explanation = explanation;
        HintFormula = hintFormula;
        HintClue = hintClue;
        HintLight = hintLight;
        HintMedium = hintMedium;
        HintFull = hintFull;
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
            ? throw new ArgumentException("Translation text is required")
            : text;
    }

    public void SetExplanation(string? explanation) => Explanation = explanation;
    public void SetHintFormula(string? hintFormula) => HintFormula = hintFormula;
    public void SetHintClue(string? hintClue) => HintClue = hintClue;
    public void SetHintLight(string? hintLight) => HintLight = hintLight;
    public void SetHintMedium(string? hintMedium) => HintMedium = hintMedium;
    public void SetHintFull(string? hintFull) => HintFull = hintFull;
}
