namespace MathLearning.Domain.Entities;

public sealed class ServerSyncEvent
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;
    public string DeviceId { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string AggregateType { get; set; } = null!;
    public string AggregateId { get; set; } = null!;
    public Guid? SourceOperationId { get; set; }
    public string PayloadJson { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
