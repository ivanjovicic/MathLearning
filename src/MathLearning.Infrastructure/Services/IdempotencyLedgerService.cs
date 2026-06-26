using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services;

public sealed class IdempotencyLedgerService : IIdempotencyLedgerService
{
    private readonly ApiDbContext db;
    private readonly ILogger<IdempotencyLedgerService> logger;
    private readonly IdempotencyObservabilityService observability;

    public IdempotencyLedgerService(
        ApiDbContext db,
        ILogger<IdempotencyLedgerService> logger,
        IdempotencyObservabilityService observability)
    {
        this.db = db;
        this.logger = logger;
        this.observability = observability;
    }

    public async Task<IdempotencyLedgerBeginResult> BeginOrGetExistingAsync(
        string userId,
        string operationType,
        string operationId,
        string idempotencyKey,
        string endpoint,
        object? requestPayload,
        CancellationToken cancellationToken = default)
    {
        var effectiveUserId = IdempotencyPayloadCanonicalizer.RequireValue(userId, nameof(userId));
        var effectiveOperationType = IdempotencyPayloadCanonicalizer.RequireValue(operationType, nameof(operationType));
        var effectiveOperationId = IdempotencyPayloadCanonicalizer.RequireValue(operationId, nameof(operationId));
        var effectiveIdempotencyKey = IdempotencyPayloadCanonicalizer.RequireValue(idempotencyKey, nameof(idempotencyKey));
        var effectiveEndpoint = IdempotencyPayloadCanonicalizer.RequireValue(endpoint, nameof(endpoint));
        var requestJson = IdempotencyPayloadCanonicalizer.CanonicalizeToJson(requestPayload);
        var payloadHash = IdempotencyPayloadCanonicalizer.ComputePayloadHash(requestJson);

        var byOperationId = await FindByOperationIdAsync(
            effectiveUserId,
            effectiveOperationType,
            effectiveOperationId,
            cancellationToken);
        var byIdempotencyKey = await FindByIdempotencyKeyAsync(
            effectiveUserId,
            effectiveOperationType,
            effectiveIdempotencyKey,
            cancellationToken);

        if (byOperationId is not null && byIdempotencyKey is not null && byOperationId.Id != byIdempotencyKey.Id)
        {
            throw new IdempotencyLedgerConflictException(
                effectiveUserId,
                effectiveOperationType,
                effectiveOperationId,
                effectiveIdempotencyKey);
        }

        var existing = byOperationId ?? byIdempotencyKey;
        if (existing is not null)
        {
            if (!string.Equals(existing.OperationId, effectiveOperationId, StringComparison.Ordinal) ||
                !string.Equals(existing.IdempotencyKey, effectiveIdempotencyKey, StringComparison.Ordinal))
            {
                observability.RecordConflict(
                    existing.Endpoint,
                    existing.OperationType,
                    effectiveOperationId,
                    effectiveUserId);
                throw new IdempotencyLedgerConflictException(
                    effectiveUserId,
                    effectiveOperationType,
                    effectiveOperationId,
                    effectiveIdempotencyKey);
            }

            return ValidateAndMapExisting(existing, payloadHash);
        }

        var now = DateTime.UtcNow;
        var ledger = new IdempotencyLedger
        {
            UserId = effectiveUserId,
            OperationType = effectiveOperationType,
            OperationId = effectiveOperationId,
            IdempotencyKey = effectiveIdempotencyKey,
            Endpoint = effectiveEndpoint,
            PayloadHash = payloadHash,
            RequestJson = requestJson,
            Status = IdempotencyLedgerStatuses.Pending,
            HttpStatus = 200,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.IdempotencyLedgers.Add(ledger);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            DetachIfTracked(ledger);

            byOperationId = await FindByOperationIdAsync(
                effectiveUserId,
                effectiveOperationType,
                effectiveOperationId,
                cancellationToken);
            byIdempotencyKey = await FindByIdempotencyKeyAsync(
                effectiveUserId,
                effectiveOperationType,
                effectiveIdempotencyKey,
                cancellationToken);

            if (byOperationId is null && byIdempotencyKey is null)
                throw;

            if (byOperationId is not null && byIdempotencyKey is not null && byOperationId.Id != byIdempotencyKey.Id)
            {
                observability.RecordConflict(
                    effectiveEndpoint,
                    effectiveOperationType,
                    effectiveOperationId,
                    effectiveUserId);
                throw new IdempotencyLedgerConflictException(
                    effectiveUserId,
                    effectiveOperationType,
                    effectiveOperationId,
                    effectiveIdempotencyKey);
            }

            existing = byOperationId ?? byIdempotencyKey;
            logger.LogDebug(ex, "Idempotency ledger creation raced. Reusing ledger {LedgerId}.", existing!.Id);
            return ValidateAndMapExisting(existing, payloadHash);
        }

        return ToBeginResult(ledger, isExisting: false, shouldProcess: true);
    }

    public async Task<IdempotencyLedgerState> CompleteAsync(
        Guid ledgerId,
        object? resultPayload,
        int httpStatus = 200,
        CancellationToken cancellationToken = default)
    {
        var ledger = await GetRequiredAsync(ledgerId, cancellationToken);
        var resultJson = IdempotencyPayloadCanonicalizer.SerializePayload(resultPayload);

        if (ledger.Status == IdempotencyLedgerStatuses.Completed)
        {
            if (!string.Equals(ledger.ResultJson, resultJson, StringComparison.Ordinal) ||
                ledger.HttpStatus != httpStatus)
            {
                throw new InvalidOperationException(
                    $"Idempotency ledger {ledgerId} is already completed with a different result payload.");
            }

            return ToState(ledger);
        }

        if (ledger.Status == IdempotencyLedgerStatuses.Failed)
        {
            throw new InvalidOperationException(
                $"Idempotency ledger {ledgerId} is already failed and cannot be completed.");
        }

        var now = DateTime.UtcNow;
        ledger.Status = IdempotencyLedgerStatuses.Completed;
        ledger.ResultJson = resultJson;
        ledger.ErrorCode = null;
        ledger.HttpStatus = httpStatus;
        ledger.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);
        observability.RecordFirstSuccess(ledger.Endpoint, ledger.OperationType, ledger.OperationId, ledger.UserId);
        return ToState(ledger);
    }

    public async Task<IdempotencyLedgerState> FailAsync(
        Guid ledgerId,
        string errorCode,
        object? resultPayload = null,
        int httpStatus = 400,
        CancellationToken cancellationToken = default)
    {
        var ledger = await GetRequiredAsync(ledgerId, cancellationToken);
        var effectiveErrorCode = IdempotencyPayloadCanonicalizer.RequireValue(errorCode, nameof(errorCode));
        var resultJson = IdempotencyPayloadCanonicalizer.SerializePayload(resultPayload);

        if (ledger.Status == IdempotencyLedgerStatuses.Failed)
        {
            if (!string.Equals(ledger.ErrorCode, effectiveErrorCode, StringComparison.Ordinal) ||
                !string.Equals(ledger.ResultJson, resultJson, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Idempotency ledger {ledgerId} is already failed with a different failure payload.");
            }

            return ToState(ledger);
        }

        if (ledger.Status == IdempotencyLedgerStatuses.Completed)
        {
            throw new InvalidOperationException(
                $"Idempotency ledger {ledgerId} is already completed and cannot be failed.");
        }

        ledger.Status = IdempotencyLedgerStatuses.Failed;
        ledger.ResultJson = resultJson;
        ledger.ErrorCode = effectiveErrorCode;
        ledger.HttpStatus = httpStatus;
        ledger.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        observability.RecordFailure(
            ledger.Endpoint,
            ledger.OperationType,
            ledger.OperationId,
            ledger.UserId,
            ledger.Status,
            ledger.ErrorCode);
        return ToState(ledger);
    }

    private async Task<IdempotencyLedger?> FindByOperationIdAsync(
        string userId,
        string operationType,
        string operationId,
        CancellationToken cancellationToken)
    {
        return await db.IdempotencyLedgers.FirstOrDefaultAsync(
            x => x.UserId == userId &&
                 x.OperationType == operationType &&
                 x.OperationId == operationId,
            cancellationToken);
    }

    private async Task<IdempotencyLedger?> FindByIdempotencyKeyAsync(
        string userId,
        string operationType,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await db.IdempotencyLedgers.FirstOrDefaultAsync(
            x => x.UserId == userId &&
                 x.OperationType == operationType &&
                 x.IdempotencyKey == idempotencyKey,
            cancellationToken);
    }

    private async Task<IdempotencyLedger> GetRequiredAsync(Guid ledgerId, CancellationToken cancellationToken)
    {
        return await db.IdempotencyLedgers.FirstOrDefaultAsync(x => x.Id == ledgerId, cancellationToken)
            ?? throw new InvalidOperationException($"Idempotency ledger {ledgerId} was not found.");
    }

    private IdempotencyLedgerBeginResult ValidateAndMapExisting(IdempotencyLedger ledger, string payloadHash)
    {
        if (!string.Equals(ledger.PayloadHash, payloadHash, StringComparison.Ordinal))
        {
            observability.RecordConflict(
                ledger.Endpoint,
                ledger.OperationType,
                ledger.OperationId,
                ledger.UserId);
            logger.LogWarning(
                "Idempotency payload conflict. UserId={UserId} OperationType={OperationType} OperationId={OperationId} IdempotencyKey={IdempotencyKey}",
                ledger.UserId,
                ledger.OperationType,
                ledger.OperationId,
                ledger.IdempotencyKey);

            throw new IdempotencyLedgerConflictException(
                ledger.UserId,
                ledger.OperationType,
                ledger.OperationId,
                ledger.IdempotencyKey);
        }

        observability.RecordReplay(
            ledger.Endpoint,
            ledger.OperationType,
            ledger.OperationId,
            ledger.UserId,
            ledger.Status);
        return ToBeginResult(ledger, isExisting: true, shouldProcess: false);
    }

    private static IdempotencyLedgerState ToState(IdempotencyLedger ledger)
    {
        return new IdempotencyLedgerState(
            ledger.Id,
            ledger.UserId,
            ledger.OperationType,
            ledger.OperationId,
            ledger.IdempotencyKey,
            ledger.Endpoint,
            ledger.Status,
            ledger.ResultJson,
            ledger.ErrorCode,
            ledger.HttpStatus,
            ledger.CreatedAtUtc,
            ledger.UpdatedAtUtc);
    }

    private static IdempotencyLedgerBeginResult ToBeginResult(
        IdempotencyLedger ledger,
        bool isExisting,
        bool shouldProcess)
    {
        return new IdempotencyLedgerBeginResult(ToState(ledger), isExisting, shouldProcess);
    }

    private void DetachIfTracked(IdempotencyLedger ledger)
    {
        var entry = db.Entry(ledger);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
    }
}
