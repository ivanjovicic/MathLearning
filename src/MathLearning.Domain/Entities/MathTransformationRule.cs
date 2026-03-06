namespace MathLearning.Domain.Entities;

public class MathTransformationRule
{
    public string Id { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string? ExpressionPattern { get; private set; }
    public string StepType { get; private set; } = "";
    public string? ExampleLatex { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private MathTransformationRule() { }

    public MathTransformationRule(string id, string name, string description, string stepType, string? expressionPattern = null, string? exampleLatex = null)
    {
        SetId(id);
        SetName(name);
        SetDescription(description);
        SetStepType(stepType);
        ExpressionPattern = string.IsNullOrWhiteSpace(expressionPattern) ? null : expressionPattern.Trim();
        ExampleLatex = string.IsNullOrWhiteSpace(exampleLatex) ? null : exampleLatex.Trim();
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

    public void SetDescription(string description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("Description is required.", nameof(description)) : description.Trim();
        Touch();
    }

    public void SetStepType(string stepType)
    {
        StepType = string.IsNullOrWhiteSpace(stepType) ? throw new ArgumentException("Step type is required.", nameof(stepType)) : stepType.Trim();
        Touch();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
