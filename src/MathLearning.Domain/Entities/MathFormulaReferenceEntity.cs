namespace MathLearning.Domain.Entities;

public class MathFormulaReferenceEntity
{
    public string Id { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Latex { get; private set; } = "";
    public string MathMl { get; private set; } = "";
    public string Description { get; private set; } = "";
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private MathFormulaReferenceEntity() { }

    public MathFormulaReferenceEntity(string id, string name, string latex, string mathMl, string description)
    {
        SetId(id);
        SetName(name);
        SetLatex(latex);
        SetMathMl(mathMl);
        SetDescription(description);
    }

    public void SetId(string id)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Id is required.", nameof(id)) : id.Trim();
        Touch();
    }

    public void SetName(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name.Trim();
        Touch();
    }

    public void SetLatex(string latex)
    {
        Latex = string.IsNullOrWhiteSpace(latex) ? throw new ArgumentException("Latex is required.", nameof(latex)) : latex.Trim();
        Touch();
    }

    public void SetMathMl(string mathMl)
    {
        MathMl = string.IsNullOrWhiteSpace(mathMl) ? throw new ArgumentException("MathML is required.", nameof(mathMl)) : mathMl.Trim();
        Touch();
    }

    public void SetDescription(string description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("Description is required.", nameof(description)) : description.Trim();
        Touch();
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
