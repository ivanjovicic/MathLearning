namespace MathLearning.Domain.Entities;

public enum EconomyTransactionStatus
{
    Pending,
    Completed,
    Failed
}

public class EconomyTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public EconomyTransactionStatus Status { get; set; } = EconomyTransactionStatus.Pending;
    public string RequestHash { get; set; } = string.Empty;
    public string RequestJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
