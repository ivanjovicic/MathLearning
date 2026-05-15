namespace MathLearning.Domain.Entities;

public class DailyRunChestClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public DateOnly Day { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string RewardSnapshotJson { get; set; } = "{}";
    public string ResponseSnapshotJson { get; set; } = "{}";
    public DateTime ClaimedAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "claimed";
    public string ResultCode { get; set; } = "ok";
}

