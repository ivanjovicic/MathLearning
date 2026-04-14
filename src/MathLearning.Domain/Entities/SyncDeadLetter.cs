namespace MathLearning.Domain.Entities;

public sealed class SyncDeadLetter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long? SyncEventLogId { get; set; }
    public Guid OperationId { get; set; }
    public string DeviceId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string OperationType { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public int RetryCount { get; set; }
    public string Status { get; set; } = SyncDeadLetterStatuses.Pending;
    public string FailureReason { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastFailedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastRedriveAttemptAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNote { get; set; }
}

public static class SyncDeadLetterStatuses
{
    public const string Pending = "Pending";
    public const string Reprocessing = "Reprocessing";
    public const string Resolved = "Resolved";
    public const string Exhausted = "Exhausted";
}
