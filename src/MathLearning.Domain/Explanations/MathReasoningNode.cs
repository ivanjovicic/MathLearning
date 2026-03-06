namespace MathLearning.Domain.Explanations;

public sealed class MathReasoningNode
{
    private readonly List<MathReasoningNode> _childrenNodes = [];

    public Guid Id { get; }
    public string Expression { get; }
    public string? LatexExpression { get; }
    public string? MathMlExpression { get; }
    public ReasoningRule RuleApplied { get; }
    public MathReasoningNode? ParentNode { get; private set; }
    public IReadOnlyList<MathReasoningNode> ChildrenNodes => _childrenNodes;
    public MathContext Context { get; }
    public string? Narrative { get; }
    public string? FormulaReferenceId { get; }

    public MathReasoningNode(
        string expression,
        ReasoningRule ruleApplied,
        MathContext context,
        string? narrative = null,
        string? latexExpression = null,
        string? mathMlExpression = null,
        string? formulaReferenceId = null,
        Guid? id = null)
    {
        Expression = string.IsNullOrWhiteSpace(expression) ? throw new ArgumentException("Expression is required.", nameof(expression)) : expression.Trim();
        RuleApplied = ruleApplied;
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Narrative = string.IsNullOrWhiteSpace(narrative) ? null : narrative.Trim();
        LatexExpression = string.IsNullOrWhiteSpace(latexExpression) ? null : latexExpression.Trim();
        MathMlExpression = string.IsNullOrWhiteSpace(mathMlExpression) ? null : mathMlExpression.Trim();
        FormulaReferenceId = string.IsNullOrWhiteSpace(formulaReferenceId) ? null : formulaReferenceId.Trim();
        Id = id ?? Guid.NewGuid();
    }

    public void AddChild(MathReasoningNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        child.ParentNode = this;
        _childrenNodes.Add(child);
    }
}
