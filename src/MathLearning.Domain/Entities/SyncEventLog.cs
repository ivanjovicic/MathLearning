namespace MathLearning.Domain.Entities;

public sealed class SyncEventLog
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public string DeviceId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public long ClientSequence { get; set; }
    public string OperationType { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public string Status { get; set; } = SyncEventStatuses.Received;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class SyncEventStatuses
{
    public const string Received = "Received";
    public const string Processed = "Processed";
    public const string Rejected = "Rejected";
    public const string Failed = "Failed";
    public const string DeadLettered = "DeadLettered";
}
