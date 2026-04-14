namespace MathLearning.Domain.Entities;

public sealed class DesignTokenAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? VersionId { get; set; }
    public DesignTokenVersion? Version { get; set; }
    public Guid? TokenSetId { get; set; }
    public DesignTokenSet? TokenSet { get; set; }
    public string Action { get; set; } = null!;
    public string? Theme { get; set; }
    public string? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? CorrelationId { get; set; }
    public string? BeforeSnapshotJson { get; set; }
    public string? AfterSnapshotJson { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
