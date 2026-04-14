namespace MathLearning.Domain.Entities;

/// <summary>
/// Immutable audit record of suspicious XP events detected by the anti-cheat system.
/// Uses flag-and-log strategy: XP is still applied but the event is recorded for review.
/// Rows are never updated or deleted.
/// </summary>
public class XpCheatLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public int XpDelta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
}
