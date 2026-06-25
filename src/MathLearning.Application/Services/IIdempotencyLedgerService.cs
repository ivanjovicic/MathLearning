namespace MathLearning.Application.Services;

using MathLearning.Domain.Entities;

public interface IIdempotencyLedgerService
{
    Task<IdempotencyLedgerBeginResult> BeginOrGetExistingAsync(
        string userId,
        string operationType,
        string operationId,
        string idempotencyKey,
        string endpoint,
        object? requestPayload,
        CancellationToken cancellationToken = default);

    Task<IdempotencyLedgerState> CompleteAsync(
        Guid ledgerId,
        object? resultPayload,
        int httpStatus = 200,
        CancellationToken cancellationToken = default);

    Task<IdempotencyLedgerState> FailAsync(
        Guid ledgerId,
        string errorCode,
        object? resultPayload = null,
        int httpStatus = 400,
        CancellationToken cancellationToken = default);
}

public sealed record IdempotencyLedgerState(
    Guid LedgerId,
    string UserId,
    string OperationType,
    string OperationId,
    string IdempotencyKey,
    string Endpoint,
    string Status,
    string? ResultJson,
    string? ErrorCode,
    int HttpStatus,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public bool IsCompleted => Status == IdempotencyLedgerStatuses.Completed;
    public bool IsPending => Status == IdempotencyLedgerStatuses.Pending;
    public bool IsFailed => Status == IdempotencyLedgerStatuses.Failed;
}

public sealed record IdempotencyLedgerBeginResult(
    IdempotencyLedgerState Ledger,
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

public sealed class IdempotencyLedgerConflictException : Exception
{
    public IdempotencyLedgerConflictException(
        string userId,
        string operationType,
        string operationId,
        string idempotencyKey)
        : base("Idempotency keys were reused with a different request payload.")
    {
        UserId = userId;
        OperationType = operationType;
        OperationId = operationId;
        IdempotencyKey = idempotencyKey;
    }

    public string UserId { get; }
    public string OperationType { get; }
    public string OperationId { get; }
    public string IdempotencyKey { get; }
}
