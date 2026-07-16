namespace MathLearning.Domain.Entities;

public class CosmeticCatalogRevision
{
    public int Id { get; set; }
    public string RevisionKey { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public string AppliedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
}
