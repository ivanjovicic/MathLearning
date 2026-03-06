namespace MathLearning.Domain.Entities;

public class CommonMistakePattern
{
    public Guid Id { get; private set; }
    public string Topic { get; private set; } = "";
    public string? Subtopic { get; private set; }
    public string MistakeType { get; private set; } = "";
    public string PatternKey { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string Remediation { get; private set; } = "";
    public int Priority { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private CommonMistakePattern() { }

    public CommonMistakePattern(string topic, string? subtopic, string mistakeType, string patternKey, string description, string remediation, int priority = 100)
    {
        Id = Guid.NewGuid();
        Topic = string.IsNullOrWhiteSpace(topic) ? throw new ArgumentException("Topic is required.", nameof(topic)) : topic.Trim();
        Subtopic = string.IsNullOrWhiteSpace(subtopic) ? null : subtopic.Trim();
        MistakeType = string.IsNullOrWhiteSpace(mistakeType) ? throw new ArgumentException("Mistake type is required.", nameof(mistakeType)) : mistakeType.Trim();
        PatternKey = string.IsNullOrWhiteSpace(patternKey) ? throw new ArgumentException("Pattern key is required.", nameof(patternKey)) : patternKey.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("Description is required.", nameof(description)) : description.Trim();
        Remediation = string.IsNullOrWhiteSpace(remediation) ? throw new ArgumentException("Remediation is required.", nameof(remediation)) : remediation.Trim();
        Priority = priority;
    }
}
