namespace MathLearning.Domain.Entities;

public sealed class DesignTokenVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Version { get; set; } = "1.0.0";
    public string Status { get; set; } = DesignTokenVersionStatuses.Draft;
    public int BaseWidth { get; set; } = 390;
    public bool IsCurrent { get; set; }
    public Guid? SourceVersionId { get; set; }
    public DesignTokenVersion? SourceVersion { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? PublishedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public ICollection<DesignTokenSet> TokenSets { get; set; } = new List<DesignTokenSet>();
    public ICollection<DesignTokenAuditLog> AuditLogs { get; set; } = new List<DesignTokenAuditLog>();
}

public static class DesignTokenVersionStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Archived = "Archived";
    public const string RolledBack = "RolledBack";
}
