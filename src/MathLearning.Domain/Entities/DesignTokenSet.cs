namespace MathLearning.Domain.Entities;

public sealed class DesignTokenSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VersionId { get; set; }
    public DesignTokenVersion Version { get; set; } = null!;
    public string Theme { get; set; } = "light";
    public string CompiledPayloadJson { get; set; } = "{}";
    public string? PayloadHash { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<DesignToken> Tokens { get; set; } = new List<DesignToken>();
}
