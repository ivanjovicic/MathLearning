namespace MathLearning.Domain.Explanations;

public sealed class FormulaReferenceDefinition
{
    public string Id { get; }
    public string Name { get; }
    public string Latex { get; }
    public string MathMl { get; }
    public string Description { get; }

    public FormulaReferenceDefinition(string id, string name, string latex, string mathMl, string description)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Formula id is required.", nameof(id)) : id.Trim();
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Formula name is required.", nameof(name)) : name.Trim();
        Latex = string.IsNullOrWhiteSpace(latex) ? throw new ArgumentException("Formula LaTeX is required.", nameof(latex)) : latex.Trim();
        MathMl = string.IsNullOrWhiteSpace(mathMl) ? throw new ArgumentException("Formula MathML is required.", nameof(mathMl)) : mathMl.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("Formula description is required.", nameof(description)) : description.Trim();
    }
}
