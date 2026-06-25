namespace MathLearning.Domain.Entities;

public class IdempotencyLedger
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string RequestJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public string? ErrorCode { get; set; }
    public string Status { get; set; } = IdempotencyLedgerStatuses.Pending;
    public int HttpStatus { get; set; } = 200;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class IdempotencyLedgerStatuses
{
    public const string Pending = "pending";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public static class QuizOperationTypes
{
    public const string QuizAnswer = "quiz_answer";
    public const string SrsUpdate = "srs_update";
}
