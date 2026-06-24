namespace MathLearning.Application.Services;

public interface ICosmeticsIdempotencyService
{
    Task<CosmeticsIdempotencyBeginResult> BeginOrGetExistingAsync(
        string userId,
        string operationType,
        string operationId,
        string idempotencyKey,
        object? requestPayload,
        CancellationToken cancellationToken = default);

    Task<CosmeticsIdempotencyState> CompleteAsync(
        Guid ledgerId,
        object? resultPayload,
        CancellationToken cancellationToken = default);

    Task<CosmeticsIdempotencyState> FailAsync(
        Guid ledgerId,
        string errorCode,
        object? resultPayload = null,
        CancellationToken cancellationToken = default);
}

public sealed record CosmeticsIdempotencyState(
    Guid LedgerId,
    string UserId,
    string OperationType,
    string OperationId,
    string IdempotencyKey,
    string Status,
    string? ResultJson,
    string? ErrorCode,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime UpdatedAtUtc)
{
    public bool IsCompleted => Status == CosmeticsIdempotencyStatuses.Completed;
    public bool IsPending => Status == CosmeticsIdempotencyStatuses.Pending;
    public bool IsFailed => Status == CosmeticsIdempotencyStatuses.Failed;
}

public static class CosmeticsIdempotencyStatuses
{
    public const string Pending = "pending";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public sealed record CosmeticsIdempotencyBeginResult(
    CosmeticsIdempotencyState Ledger,
    bool IsExisting,
    bool ShouldProcess)
{
    public Guid LedgerId => Ledger.LedgerId;
    public string? ResultJson => Ledger.ResultJson;
    public string? ErrorCode => Ledger.ErrorCode;
    public bool IsCompleted => Ledger.IsCompleted;
    public bool IsPending => Ledger.IsPending;
    public bool IsFailed => Ledger.IsFailed;
}

public sealed class CosmeticsIdempotencyConflictException : Exception
{
    public CosmeticsIdempotencyConflictException(string userId, string operationId, string idempotencyKey)
        : base($"Cosmetics idempotency keys were reused with a different request payload.")
    {
        UserId = userId;
        OperationId = operationId;
        IdempotencyKey = idempotencyKey;
    }

    public string UserId { get; }
    public string OperationId { get; }
    public string IdempotencyKey { get; }
}
