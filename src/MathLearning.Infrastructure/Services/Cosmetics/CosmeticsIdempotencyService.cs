using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public sealed class CosmeticsIdempotencyService : ICosmeticsIdempotencyService
{
    private readonly ApiDbContext db;
    private readonly ILogger<CosmeticsIdempotencyService> logger;
    private readonly IdempotencyObservabilityService observability;

    public CosmeticsIdempotencyService(
        ApiDbContext db,
        ILogger<CosmeticsIdempotencyService> logger,
        IdempotencyObservabilityService observability)
    {
        this.db = db;
        this.logger = logger;
        this.observability = observability;
    }

    public async Task<CosmeticsIdempotencyBeginResult> BeginOrGetExistingAsync(
        string userId,
        string operationType,
        string operationId,
        string idempotencyKey,
        object? requestPayload,
        CancellationToken cancellationToken = default)
    {
        var effectiveUserId = IdempotencyPayloadCanonicalizer.RequireValue(userId, nameof(userId));
        var effectiveOperationType = IdempotencyPayloadCanonicalizer.RequireValue(operationType, nameof(operationType));
        var effectiveOperationId = IdempotencyPayloadCanonicalizer.RequireValue(operationId, nameof(operationId));
        var effectiveIdempotencyKey = IdempotencyPayloadCanonicalizer.RequireValue(idempotencyKey, nameof(idempotencyKey));
        var requestJson = IdempotencyPayloadCanonicalizer.CanonicalizeToJson(requestPayload);
        var payloadHash = IdempotencyPayloadCanonicalizer.ComputePayloadHash(requestJson);

        var byOperationId = await FindByOperationIdAsync(effectiveUserId, effectiveOperationId, cancellationToken);
        var byIdempotencyKey = await FindByIdempotencyKeyAsync(effectiveUserId, effectiveIdempotencyKey, cancellationToken);

        if (byOperationId is not null && byIdempotencyKey is not null && byOperationId.Id != byIdempotencyKey.Id)
        {
            observability.RecordConflict(
                IdempotencyObservabilityService.ResolveCosmeticsEndpoint(effectiveOperationType),
                effectiveOperationType,
                effectiveOperationId,
                effectiveUserId);
            throw new CosmeticsIdempotencyConflictException(
                effectiveUserId,
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
                    IdempotencyObservabilityService.ResolveCosmeticsEndpoint(effectiveOperationType),
                    effectiveOperationType,
                    effectiveOperationId,
                    effectiveUserId);
                throw new CosmeticsIdempotencyConflictException(
                    effectiveUserId,
                    effectiveOperationId,
                    effectiveIdempotencyKey);
            }

            return ValidateAndMapExisting(existing, payloadHash);
        }

        var now = DateTime.UtcNow;
        var ledger = new CosmeticsIdempotencyLedger
        {
            UserId = effectiveUserId,
            OperationType = effectiveOperationType,
            OperationId = effectiveOperationId,
            IdempotencyKey = effectiveIdempotencyKey,
            PayloadHash = payloadHash,
            RequestJson = requestJson,
            Status = CosmeticsIdempotencyStatuses.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.CosmeticsIdempotencyLedgers.Add(ledger);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            DetachIfTracked(ledger);

            byOperationId = await FindByOperationIdAsync(effectiveUserId, effectiveOperationId, cancellationToken);
            byIdempotencyKey = await FindByIdempotencyKeyAsync(effectiveUserId, effectiveIdempotencyKey, cancellationToken);

            if (byOperationId is null && byIdempotencyKey is null)
                throw;

            if (byOperationId is not null && byIdempotencyKey is not null && byOperationId.Id != byIdempotencyKey.Id)
            {
                throw new CosmeticsIdempotencyConflictException(
                    effectiveUserId,
                    effectiveOperationId,
                    effectiveIdempotencyKey);
            }

            existing = byOperationId ?? byIdempotencyKey;
            logger.LogDebug(ex, "Cosmetics idempotency creation raced. Reusing ledger {LedgerId}.", existing!.Id);
            return ValidateAndMapExisting(existing, payloadHash);
        }

        return ToBeginResult(ledger, isExisting: false, shouldProcess: true);
    }

    public async Task<CosmeticsIdempotencyState> CompleteAsync(
        Guid ledgerId,
        object? resultPayload,
        CancellationToken cancellationToken = default)
    {
        var ledger = await GetRequiredAsync(ledgerId, cancellationToken);
        var resultJson = IdempotencyPayloadCanonicalizer.SerializePayload(resultPayload);

        if (ledger.Status == CosmeticsIdempotencyStatuses.Completed)
        {
            if (!string.Equals(ledger.ResultJson, resultJson, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Cosmetics idempotency ledger {ledgerId} is already completed with a different result payload.");
            }

            return ToState(ledger);
        }

        if (ledger.Status == CosmeticsIdempotencyStatuses.Failed)
        {
            throw new InvalidOperationException(
                $"Cosmetics idempotency ledger {ledgerId} is already failed and cannot be completed.");
        }

        var now = DateTime.UtcNow;
        ledger.Status = CosmeticsIdempotencyStatuses.Completed;
        ledger.ResultJson = resultJson;
        ledger.ErrorCode = null;
        ledger.CompletedAtUtc = now;
        ledger.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);
        observability.RecordFirstSuccess(
            IdempotencyObservabilityService.ResolveCosmeticsEndpoint(ledger.OperationType),
            ledger.OperationType,
            ledger.OperationId,
            ledger.UserId);
        return ToState(ledger);
    }

    public async Task<CosmeticsIdempotencyState> FailAsync(
        Guid ledgerId,
        string errorCode,
        object? resultPayload = null,
        CancellationToken cancellationToken = default)
    {
        var ledger = await GetRequiredAsync(ledgerId, cancellationToken);
        var effectiveErrorCode = IdempotencyPayloadCanonicalizer.RequireValue(errorCode, nameof(errorCode));
        var resultJson = IdempotencyPayloadCanonicalizer.SerializePayload(resultPayload);

        if (ledger.Status == CosmeticsIdempotencyStatuses.Failed)
        {
            if (!string.Equals(ledger.ErrorCode, effectiveErrorCode, StringComparison.Ordinal) ||
                !string.Equals(ledger.ResultJson, resultJson, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Cosmetics idempotency ledger {ledgerId} is already failed with a different failure payload.");
            }

            return ToState(ledger);
        }

        if (ledger.Status == CosmeticsIdempotencyStatuses.Completed)
        {
            throw new InvalidOperationException(
                $"Cosmetics idempotency ledger {ledgerId} is already completed and cannot be failed.");
        }

        ledger.Status = CosmeticsIdempotencyStatuses.Failed;
        ledger.ResultJson = resultJson;
        ledger.ErrorCode = effectiveErrorCode;
        ledger.CompletedAtUtc = null;
        ledger.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        observability.RecordFailure(
            IdempotencyObservabilityService.ResolveCosmeticsEndpoint(ledger.OperationType),
            ledger.OperationType,
            ledger.OperationId,
            ledger.UserId,
            ledger.Status,
            ledger.ErrorCode);
        return ToState(ledger);
    }

    private async Task<CosmeticsIdempotencyLedger?> FindByOperationIdAsync(
        string userId,
        string operationId,
        CancellationToken cancellationToken)
    {
        return await db.CosmeticsIdempotencyLedgers.FirstOrDefaultAsync(
            x => x.UserId == userId && x.OperationId == operationId,
            cancellationToken);
    }

    private async Task<CosmeticsIdempotencyLedger?> FindByIdempotencyKeyAsync(
        string userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await db.CosmeticsIdempotencyLedgers.FirstOrDefaultAsync(
            x => x.UserId == userId && x.IdempotencyKey == idempotencyKey,
            cancellationToken);
    }

    private async Task<CosmeticsIdempotencyLedger> GetRequiredAsync(Guid ledgerId, CancellationToken cancellationToken)
    {
        return await db.CosmeticsIdempotencyLedgers.FirstOrDefaultAsync(x => x.Id == ledgerId, cancellationToken)
            ?? throw new InvalidOperationException($"Cosmetics idempotency ledger {ledgerId} was not found.");
    }

    private CosmeticsIdempotencyBeginResult ValidateAndMapExisting(CosmeticsIdempotencyLedger ledger, string payloadHash)
    {
        if (!string.Equals(ledger.PayloadHash, payloadHash, StringComparison.Ordinal))
        {
            observability.RecordConflict(
                IdempotencyObservabilityService.ResolveCosmeticsEndpoint(ledger.OperationType),
                ledger.OperationType,
                ledger.OperationId,
                ledger.UserId);
            logger.LogWarning(
                "Cosmetics idempotency payload conflict. UserId={UserId} OperationId={OperationId} IdempotencyKey={IdempotencyKey}",
                ledger.UserId,
                ledger.OperationId,
                ledger.IdempotencyKey);

            throw new CosmeticsIdempotencyConflictException(
                ledger.UserId,
                ledger.OperationId,
                ledger.IdempotencyKey);
        }

        observability.RecordReplay(
            IdempotencyObservabilityService.ResolveCosmeticsEndpoint(ledger.OperationType),
            ledger.OperationType,
            ledger.OperationId,
            ledger.UserId,
            ledger.Status);
        return ToBeginResult(ledger, isExisting: true, shouldProcess: false);
    }

    private static CosmeticsIdempotencyState ToState(CosmeticsIdempotencyLedger ledger)
    {
        return new CosmeticsIdempotencyState(
            ledger.Id,
            ledger.UserId,
            ledger.OperationType,
            ledger.OperationId,
            ledger.IdempotencyKey,
            ledger.Status,
            ledger.ResultJson,
            ledger.ErrorCode,
            ledger.CreatedAtUtc,
            ledger.CompletedAtUtc,
            ledger.UpdatedAtUtc);
    }

    private static CosmeticsIdempotencyBeginResult ToBeginResult(
        CosmeticsIdempotencyLedger ledger,
        bool isExisting,
        bool shouldProcess)
    {
        return new CosmeticsIdempotencyBeginResult(ToState(ledger), isExisting, shouldProcess);
    }

    private void DetachIfTracked(CosmeticsIdempotencyLedger ledger)
    {
        var entry = db.Entry(ledger);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
    }
}
