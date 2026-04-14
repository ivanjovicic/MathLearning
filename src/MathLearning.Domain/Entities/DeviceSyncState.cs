namespace MathLearning.Domain.Entities;

public sealed class DeviceSyncState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public long LastAcknowledgedEvent { get; set; }
    public long LastProcessedClientSequence { get; set; }
    public DateTime LastSyncTimeUtc { get; set; } = DateTime.UtcNow;
    public string? LastBundleVersion { get; set; }
}
