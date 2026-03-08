namespace MathLearning.Domain.Entities;

public sealed class DesignToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TokenSetId { get; set; }
    public DesignTokenSet TokenSet { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string TokenKey { get; set; } = null!;
    public string ValueJson { get; set; } = null!;
    public string ValueType { get; set; } = null!;
    public string Source { get; set; } = "database";
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
