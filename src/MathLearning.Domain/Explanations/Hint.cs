namespace MathLearning.Domain.Explanations;

public sealed class Hint
{
    public const int MaxTextLength = 500;

    public string Text { get; }
    public HintType HintType { get; }
    public int RevealOrder { get; }

    public Hint(string text, HintType hintType, int revealOrder = 1)
    {
        text = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Hint text is required.", nameof(text));
        if (text.Length > MaxTextLength)
            throw new ArgumentOutOfRangeException(nameof(text), $"Hint text must be at most {MaxTextLength} characters.");
        if (revealOrder <= 0)
            throw new ArgumentOutOfRangeException(nameof(revealOrder), "Reveal order must be positive.");

        Text = text;
        HintType = hintType;
        RevealOrder = revealOrder;
    }
}
