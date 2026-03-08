namespace MathLearning.Domain.Entities;

public sealed class SyncDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string? DeviceName { get; set; }
    public string Platform { get; set; } = "unknown";
    public string? AppVersion { get; set; }
    public string SecretKey { get; set; } = null!;
    public string Status { get; set; } = SyncDeviceStatuses.Active;
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}

public static class SyncDeviceStatuses
{
    public const string Active = "Active";
    public const string Revoked = "Revoked";
}
