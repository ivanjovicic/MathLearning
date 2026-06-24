using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public sealed class CosmeticsIdempotencyService : ICosmeticsIdempotencyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiDbContext db;
    private readonly ILogger<CosmeticsIdempotencyService> logger;

    public CosmeticsIdempotencyService(ApiDbContext db, ILogger<CosmeticsIdempotencyService> logger)
    {
        this.db = db;
        this.logger = logger;
    }

    public async Task<CosmeticsIdempotencyBeginResult> BeginOrGetExistingAsync(
        string userId,
        string operationType,
        string operationId,
        string idempotencyKey,
        object? requestPayload,
        CancellationToken cancellationToken = default)
    {
        var effectiveUserId = RequireValue(userId, nameof(userId));
        var effectiveOperationType = RequireValue(operationType, nameof(operationType));
        var effectiveOperationId = RequireValue(operationId, nameof(operationId));
        var effectiveIdempotencyKey = RequireValue(idempotencyKey, nameof(idempotencyKey));
        var requestJson = CanonicalizeToJson(requestPayload);
        var payloadHash = ComputePayloadHash(requestJson);

        var byOperationId = await FindByOperationIdAsync(effectiveUserId, effectiveOperationId, cancellationToken);
        var byIdempotencyKey = await FindByIdempotencyKeyAsync(effectiveUserId, effectiveIdempotencyKey, cancellationToken);

        if (byOperationId is not null && byIdempotencyKey is not null && byOperationId.Id != byIdempotencyKey.Id)
        {
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
        var resultJson = SerializePayload(resultPayload);

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
        return ToState(ledger);
    }

    public async Task<CosmeticsIdempotencyState> FailAsync(
        Guid ledgerId,
        string errorCode,
        object? resultPayload = null,
        CancellationToken cancellationToken = default)
    {
        var ledger = await GetRequiredAsync(ledgerId, cancellationToken);
        var effectiveErrorCode = RequireValue(errorCode, nameof(errorCode));
        var resultJson = SerializePayload(resultPayload);

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

    private static string RequireValue(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);

        return value.Trim();
    }

    private static string ComputePayloadHash(string canonicalJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(bytes);
    }

    private static string? SerializePayload(object? payload)
    {
        return payload is null ? null : CanonicalizeToJson(payload);
    }

    private static string CanonicalizeToJson(object? payload)
    {
        var element = payload is JsonElement jsonElement
            ? jsonElement.Clone()
            : JsonSerializer.SerializeToElement(payload, JsonOptions);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonicalJson(element, writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteCanonicalJson(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(property.Value, writer);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalJson(item, writer);
                }

                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
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
