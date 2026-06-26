using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services;

public sealed class EconomyTransactionService : IEconomyTransactionService
{
    private readonly ApiDbContext db;
    private readonly ILogger<EconomyTransactionService> logger;
    private readonly IdempotencyObservabilityService observability;

    public EconomyTransactionService(
        ApiDbContext db,
        ILogger<EconomyTransactionService> logger,
        IdempotencyObservabilityService observability)
    {
        this.db = db;
        this.logger = logger;
        this.observability = observability;
    }

    public async Task<EconomyTransactionBeginResult> BeginOrGetExistingAsync(
        string userId,
        string transactionType,
        string idempotencyKey,
        object? requestPayload,
        string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveUserId = IdempotencyPayloadCanonicalizer.RequireValue(userId, nameof(userId));
        var effectiveTransactionType = IdempotencyPayloadCanonicalizer.RequireValue(transactionType, nameof(transactionType));
        var effectiveIdempotencyKey = IdempotencyPayloadCanonicalizer.RequireValue(idempotencyKey, nameof(idempotencyKey));
        var effectiveOperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
        var requestJson = IdempotencyPayloadCanonicalizer.CanonicalizeToJson(requestPayload);
        var requestHash = IdempotencyPayloadCanonicalizer.ComputePayloadHash(requestJson);

        var byIdempotencyKey = await FindByIdempotencyKeyAsync(
            effectiveUserId,
            effectiveTransactionType,
            effectiveIdempotencyKey,
            cancellationToken);

        EconomyTransaction? byOperationId = null;
        if (effectiveOperationId is not null)
        {
            byOperationId = await FindByOperationIdAsync(
                effectiveUserId,
                effectiveTransactionType,
                effectiveOperationId,
                cancellationToken);
        }

        if (byIdempotencyKey is not null && byOperationId is not null && byIdempotencyKey.Id != byOperationId.Id)
        {
            observability.RecordConflict(
                IdempotencyObservabilityService.ResolveEconomyEndpoint(effectiveTransactionType),
                effectiveTransactionType,
                effectiveOperationId ?? effectiveIdempotencyKey,
                effectiveUserId);
            logger.LogWarning(
                "Economy transaction key conflict detected. UserId={UserId} TransactionType={TransactionType} IdempotencyKey={IdempotencyKey} OperationId={OperationId}",
                effectiveUserId,
                effectiveTransactionType,
                effectiveIdempotencyKey,
                effectiveOperationId);

            throw new EconomyTransactionConflictException(
                effectiveUserId,
                effectiveTransactionType,
                effectiveIdempotencyKey);
        }

        var existing = byIdempotencyKey ?? byOperationId;
        if (existing is not null)
        {
            if (effectiveOperationId is not null &&
                !string.IsNullOrWhiteSpace(existing.OperationId) &&
                !string.Equals(existing.OperationId, effectiveOperationId, StringComparison.Ordinal))
            {
                observability.RecordConflict(
                    IdempotencyObservabilityService.ResolveEconomyEndpoint(effectiveTransactionType),
                    effectiveTransactionType,
                    effectiveOperationId,
                    effectiveUserId);
                throw new EconomyTransactionConflictException(
                    effectiveUserId,
                    effectiveTransactionType,
                    effectiveIdempotencyKey);
            }

            if (!string.Equals(existing.IdempotencyKey, effectiveIdempotencyKey, StringComparison.Ordinal))
            {
                observability.RecordConflict(
                    IdempotencyObservabilityService.ResolveEconomyEndpoint(effectiveTransactionType),
                    effectiveTransactionType,
                    effectiveOperationId ?? effectiveIdempotencyKey,
                    effectiveUserId);
                throw new EconomyTransactionConflictException(
                    effectiveUserId,
                    effectiveTransactionType,
                    effectiveIdempotencyKey);
            }

            return ValidateAndMapExisting(existing, requestHash);
        }

        var now = DateTime.UtcNow;
        var transaction = new EconomyTransaction
        {
            UserId = effectiveUserId,
            TransactionType = effectiveTransactionType,
            IdempotencyKey = effectiveIdempotencyKey,
            OperationId = effectiveOperationId,
            Status = EconomyTransactionStatus.Pending,
            RequestHash = requestHash,
            RequestJson = requestJson,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.EconomyTransactions.Add(transaction);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            DetachIfTracked(transaction);

            byIdempotencyKey = await FindByIdempotencyKeyAsync(
                effectiveUserId,
                effectiveTransactionType,
                effectiveIdempotencyKey,
                cancellationToken);
            byOperationId = effectiveOperationId is null
                ? null
                : await FindByOperationIdAsync(
                    effectiveUserId,
                    effectiveTransactionType,
                    effectiveOperationId,
                    cancellationToken);

            if (byIdempotencyKey is null && byOperationId is null)
                throw;

            if (byIdempotencyKey is not null && byOperationId is not null && byIdempotencyKey.Id != byOperationId.Id)
            {
                throw new EconomyTransactionConflictException(
                    effectiveUserId,
                    effectiveTransactionType,
                    effectiveIdempotencyKey);
            }

            existing = byIdempotencyKey ?? byOperationId;
            logger.LogDebug(
                ex,
                "Economy transaction creation raced with another request. Reusing transaction {TransactionId}.",
                existing!.Id);

            return ValidateAndMapExisting(existing, requestHash);
        }

        return ToBeginResult(transaction, isExisting: false, shouldProcess: true);
    }

    public async Task<EconomyTransactionState> CompleteAsync(
        Guid transactionId,
        object? resultPayload,
        CancellationToken cancellationToken = default)
    {
        var transaction = await GetRequiredAsync(transactionId, cancellationToken);
        var resultJson = IdempotencyPayloadCanonicalizer.SerializePayload(resultPayload);

        if (transaction.Status == EconomyTransactionStatus.Completed)
        {
            if (!string.Equals(transaction.ResultJson, resultJson, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Economy transaction {transactionId} is already completed with a different result payload.");
            }

            return ToState(transaction);
        }

        if (transaction.Status == EconomyTransactionStatus.Failed)
        {
            throw new InvalidOperationException(
                $"Economy transaction {transactionId} is already failed and cannot be completed.");
        }

        var now = DateTime.UtcNow;
        transaction.Status = EconomyTransactionStatus.Completed;
        transaction.ResultJson = resultJson;
        transaction.ErrorCode = null;
        transaction.CompletedAtUtc = now;
        transaction.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);
        observability.RecordFirstSuccess(
            IdempotencyObservabilityService.ResolveEconomyEndpoint(transaction.TransactionType),
            transaction.TransactionType,
            transaction.OperationId ?? transaction.IdempotencyKey,
            transaction.UserId);
        return ToState(transaction);
    }

    public async Task<EconomyTransactionState> FailAsync(
        Guid transactionId,
        string errorCode,
        object? resultPayload = null,
        CancellationToken cancellationToken = default)
    {
        var transaction = await GetRequiredAsync(transactionId, cancellationToken);
        var effectiveErrorCode = IdempotencyPayloadCanonicalizer.RequireValue(errorCode, nameof(errorCode));
        var resultJson = IdempotencyPayloadCanonicalizer.SerializePayload(resultPayload);

        if (transaction.Status == EconomyTransactionStatus.Failed)
        {
            if (!string.Equals(transaction.ErrorCode, effectiveErrorCode, StringComparison.Ordinal) ||
                !string.Equals(transaction.ResultJson, resultJson, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Economy transaction {transactionId} is already failed with a different failure payload.");
            }

            return ToState(transaction);
        }

        if (transaction.Status == EconomyTransactionStatus.Completed)
        {
            throw new InvalidOperationException(
                $"Economy transaction {transactionId} is already completed and cannot be failed.");
        }

        transaction.Status = EconomyTransactionStatus.Failed;
        transaction.ResultJson = resultJson;
        transaction.ErrorCode = effectiveErrorCode;
        transaction.CompletedAtUtc = null;
        transaction.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        observability.RecordFailure(
            IdempotencyObservabilityService.ResolveEconomyEndpoint(transaction.TransactionType),
            transaction.TransactionType,
            transaction.OperationId ?? transaction.IdempotencyKey,
            transaction.UserId,
            transaction.Status.ToString(),
            transaction.ErrorCode);
        return ToState(transaction);
    }

    private async Task<EconomyTransaction?> FindByIdempotencyKeyAsync(
        string userId,
        string transactionType,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await db.EconomyTransactions.FirstOrDefaultAsync(
            x => x.UserId == userId &&
                 x.TransactionType == transactionType &&
                 x.IdempotencyKey == idempotencyKey,
            cancellationToken);
    }

    private async Task<EconomyTransaction?> FindByOperationIdAsync(
        string userId,
        string transactionType,
        string operationId,
        CancellationToken cancellationToken)
    {
        return await db.EconomyTransactions.FirstOrDefaultAsync(
            x => x.UserId == userId &&
                 x.TransactionType == transactionType &&
                 x.OperationId == operationId,
            cancellationToken);
    }

    private async Task<EconomyTransaction> GetRequiredAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        return await db.EconomyTransactions.FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken)
            ?? throw new InvalidOperationException($"Economy transaction {transactionId} was not found.");
    }

    private EconomyTransactionBeginResult ValidateAndMapExisting(EconomyTransaction transaction, string requestHash)
    {
        if (!string.Equals(transaction.RequestHash, requestHash, StringComparison.Ordinal))
        {
            observability.RecordConflict(
                IdempotencyObservabilityService.ResolveEconomyEndpoint(transaction.TransactionType),
                transaction.TransactionType,
                transaction.OperationId ?? transaction.IdempotencyKey,
                transaction.UserId);
            logger.LogWarning(
                "Economy transaction payload conflict detected. UserId={UserId} TransactionType={TransactionType} IdempotencyKey={IdempotencyKey}",
                transaction.UserId,
                transaction.TransactionType,
                transaction.IdempotencyKey);

            throw new EconomyTransactionConflictException(
                transaction.UserId,
                transaction.TransactionType,
                transaction.IdempotencyKey);
        }

        observability.RecordReplay(
            IdempotencyObservabilityService.ResolveEconomyEndpoint(transaction.TransactionType),
            transaction.TransactionType,
            transaction.OperationId ?? transaction.IdempotencyKey,
            transaction.UserId,
            transaction.Status.ToString());
        return ToBeginResult(transaction, isExisting: true, shouldProcess: false);
    }

    private static EconomyTransactionState ToState(EconomyTransaction transaction)
    {
        return new EconomyTransactionState(
            transaction.Id,
            transaction.UserId,
            transaction.TransactionType,
            transaction.IdempotencyKey,
            transaction.Status,
            transaction.ResultJson,
            transaction.ErrorCode,
            transaction.CreatedAtUtc,
            transaction.CompletedAtUtc,
            transaction.UpdatedAtUtc);
    }

    private static EconomyTransactionBeginResult ToBeginResult(
        EconomyTransaction transaction,
        bool isExisting,
        bool shouldProcess)
    {
        return new EconomyTransactionBeginResult(ToState(transaction), isExisting, shouldProcess);
    }

    private void DetachIfTracked(EconomyTransaction transaction)
    {
        var entry = db.Entry(transaction);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
    }
}
