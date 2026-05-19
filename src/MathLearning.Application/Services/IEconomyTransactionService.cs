using MathLearning.Domain.Entities;

namespace MathLearning.Application.Services;

public interface IEconomyTransactionService
{
    Task<EconomyTransactionBeginResult> BeginOrGetExistingAsync(
        string userId,
        string transactionType,
        string idempotencyKey,
        object? requestPayload,
        CancellationToken cancellationToken = default);

    Task<EconomyTransactionState> CompleteAsync(
        Guid transactionId,
        object? resultPayload,
        CancellationToken cancellationToken = default);

    Task<EconomyTransactionState> FailAsync(
        Guid transactionId,
        string errorCode,
        object? resultPayload = null,
        CancellationToken cancellationToken = default);
}

public sealed record EconomyTransactionState(
    Guid TransactionId,
    string UserId,
    string TransactionType,
    string IdempotencyKey,
    EconomyTransactionStatus Status,
    string? ResultJson,
    string? ErrorCode,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime UpdatedAtUtc)
{
    public bool IsCompleted => Status == EconomyTransactionStatus.Completed;
    public bool IsPending => Status == EconomyTransactionStatus.Pending;
    public bool IsFailed => Status == EconomyTransactionStatus.Failed;
}

public sealed record EconomyTransactionBeginResult(
    EconomyTransactionState Transaction,
    bool IsExisting,
    bool ShouldProcess)
{
    public Guid TransactionId => Transaction.TransactionId;
    public string UserId => Transaction.UserId;
    public string TransactionType => Transaction.TransactionType;
    public string IdempotencyKey => Transaction.IdempotencyKey;
    public EconomyTransactionStatus Status => Transaction.Status;
    public string? ResultJson => Transaction.ResultJson;
    public string? ErrorCode => Transaction.ErrorCode;
    public bool IsCompleted => Transaction.IsCompleted;
    public bool IsPending => Transaction.IsPending;
    public bool IsFailed => Transaction.IsFailed;
}

public sealed class EconomyTransactionConflictException : Exception
{
    public EconomyTransactionConflictException(
        string userId,
        string transactionType,
        string idempotencyKey)
        : base($"Idempotency key '{idempotencyKey}' for transaction type '{transactionType}' was reused with a different request payload.")
    {
        UserId = userId;
        TransactionType = transactionType;
        IdempotencyKey = idempotencyKey;
    }

    public string UserId { get; }
    public string TransactionType { get; }
    public string IdempotencyKey { get; }
    public string ErrorCode => "IDEMPOTENCY_PAYLOAD_CONFLICT";
}