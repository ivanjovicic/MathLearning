namespace MathLearning.Domain.Explanations;

public sealed class StepExplanation
{
    public const int MaxTextLength = 2000;

    public Guid Id { get; }
    public int Order { get; }
    public string Text { get; }
    public StepType StepType { get; }
    public ExplanationType ExplanationType { get; }
    public bool Highlight { get; }
    public Hint? Hint { get; }
    public string? LatexExpression { get; }
    public string? MathMlExpression { get; }
    public string? FormulaReferenceId { get; }
    public DifficultyLevel Difficulty { get; }
    public MathContext Context { get; }
    public string? ImageUrl { get; }

    public StepExplanation(
        int order,
        string text,
        StepType stepType,
        ExplanationType explanationType,
        bool highlight,
        Hint? hint,
        DifficultyLevel difficulty,
        MathContext context,
        string? latexExpression = null,
        string? mathMlExpression = null,
        string? formulaReferenceId = null,
        string? imageUrl = null,
        Guid? id = null)
    {
        text = text?.Trim() ?? string.Empty;
        if (order <= 0)
            throw new ArgumentOutOfRangeException(nameof(order), "Order must be positive.");
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Step text is required.", nameof(text));
        if (text.Length > MaxTextLength)
            throw new ArgumentOutOfRangeException(nameof(text), $"Step text must be at most {MaxTextLength} characters.");

        Id = id ?? Guid.NewGuid();
        Order = order;
        Text = text;
        StepType = stepType;
        ExplanationType = explanationType;
        Highlight = highlight;
        Hint = hint;
        Difficulty = difficulty;
        Context = context ?? throw new ArgumentNullException(nameof(context));
        LatexExpression = string.IsNullOrWhiteSpace(latexExpression) ? null : latexExpression.Trim();
        MathMlExpression = string.IsNullOrWhiteSpace(mathMlExpression) ? null : mathMlExpression.Trim();
        FormulaReferenceId = string.IsNullOrWhiteSpace(formulaReferenceId) ? null : formulaReferenceId.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
    }
}
