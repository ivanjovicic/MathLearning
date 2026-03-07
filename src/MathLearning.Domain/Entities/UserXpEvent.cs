namespace MathLearning.Domain.Entities;

public class UserXpEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public int? SchoolId { get; set; }
    public int XpDelta { get; set; }
    public int ValidatedXpDelta { get; set; }
    public string SourceType { get; set; } = "manual_adjustment";
    public string? SourceId { get; set; }
    public string ValidationStatus { get; set; } = "approved";
    public bool IsSuspicious { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime AwardedAtUtc { get; set; } = DateTime.UtcNow;
}
