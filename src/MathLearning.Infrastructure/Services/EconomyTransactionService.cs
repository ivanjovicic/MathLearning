using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services;

public sealed class EconomyTransactionService : IEconomyTransactionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiDbContext db;
    private readonly ILogger<EconomyTransactionService> logger;

    public EconomyTransactionService(ApiDbContext db, ILogger<EconomyTransactionService> logger)
    {
        this.db = db;
        this.logger = logger;
    }

    public async Task<EconomyTransactionBeginResult> BeginOrGetExistingAsync(
        string userId,
        string transactionType,
        string idempotencyKey,
        object? requestPayload,
        string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveUserId = RequireValue(userId, nameof(userId));
        var effectiveTransactionType = RequireValue(transactionType, nameof(transactionType));
        var effectiveIdempotencyKey = RequireValue(idempotencyKey, nameof(idempotencyKey));
        var effectiveOperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
        var requestJson = CanonicalizeToJson(requestPayload);
        var requestHash = ComputePayloadHash(requestJson);

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
                throw new EconomyTransactionConflictException(
                    effectiveUserId,
                    effectiveTransactionType,
                    effectiveIdempotencyKey);
            }

            if (!string.Equals(existing.IdempotencyKey, effectiveIdempotencyKey, StringComparison.Ordinal))
            {
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
        var resultJson = SerializePayload(resultPayload);

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
        return ToState(transaction);
    }

    public async Task<EconomyTransactionState> FailAsync(
        Guid transactionId,
        string errorCode,
        object? resultPayload = null,
        CancellationToken cancellationToken = default)
    {
        var transaction = await GetRequiredAsync(transactionId, cancellationToken);
        var effectiveErrorCode = RequireValue(errorCode, nameof(errorCode));
        var resultJson = SerializePayload(resultPayload);

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

    private void DetachIfTracked(EconomyTransaction transaction)
    {
        var entry = db.Entry(transaction);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
    }
}
