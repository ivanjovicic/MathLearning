using System.Text.Json.Nodes;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathLearning.Api.Endpoints;

internal static class EconomyEndpointHelpers
{
    public static bool ValidateIdempotencyKey(string? key, out IResult? error)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            error = Results.BadRequest(BusinessError("invalid_idempotency_key", "IdempotencyKey is required."));
            return false;
        }

        error = null;
        return true;
    }

    public static IResult? HandleIdempotentDecision(EconomyTransactionBeginResult begin)
    {
        if (begin.ShouldProcess)
            return null;

        if (begin.IsCompleted)
            return ReplayStoredJson(begin.ResultJson, successStatusCode: StatusCodes.Status200OK);

        if (begin.IsFailed)
            return ReplayStoredJson(begin.ResultJson, successStatusCode: MapErrorStatusCode(begin.ErrorCode));

        if (begin.IsPending)
            return Results.Conflict(BusinessError("transaction_in_progress", "A matching request is already being processed."));

        return Results.Conflict(BusinessError("idempotency_conflict", "Invalid transaction state."));
    }

    public static string ResolveOperationId(string? operationId, string idempotencyKey, string? transactionId = null)
        => TrimOrNull(operationId) ?? TrimOrNull(transactionId) ?? idempotencyKey;

    public static async Task<(EconomyTransactionBeginResult? Begin, IResult? Error)> TryBeginAsync(
        IEconomyTransactionService txService,
        string userId,
        string transactionType,
        string idempotencyKey,
        object requestPayload,
        CancellationToken ct,
        string? operationId = null,
        string? transactionId = null)
    {
        try
        {
            var begin = await txService.BeginOrGetExistingAsync(
                userId,
                transactionType,
                idempotencyKey,
                requestPayload,
                operationId: ResolveOperationId(operationId, idempotencyKey, transactionId),
                cancellationToken: ct);
            return (begin, null);
        }
        catch (EconomyTransactionConflictException)
        {
            return (null, Results.Conflict(BusinessError("idempotency_conflict", "Idempotency key already exists with a different payload.")));
        }
    }

    /// <summary>
    /// Pattern A: open the ambient DB transaction before claiming the economy ledger so an abandoned
    /// request cannot leave a durable pending tombstone outside the domain transaction.
    /// </summary>
    public static async Task<(IDbContextTransaction? DbTx, EconomyTransactionBeginResult? Begin, IResult? EarlyResult)> BeginClaimInTransactionAsync(
        ApiDbContext db,
        IEconomyTransactionService txService,
        string userId,
        string transactionType,
        string idempotencyKey,
        object requestPayload,
        CancellationToken ct,
        string? operationId = null,
        string? transactionId = null)
    {
        var dbTx = await BeginDbTransactionIfSupportedAsync(db, ct);
        try
        {
            var beginTuple = await TryBeginAsync(
                txService,
                userId,
                transactionType,
                idempotencyKey,
                requestPayload,
                ct,
                operationId,
                transactionId);
            if (beginTuple.Error is not null)
            {
                if (dbTx is not null)
                    await dbTx.RollbackAsync(ct);
                return (null, null, beginTuple.Error);
            }

            var begin = beginTuple.Begin!;
            var early = HandleIdempotentDecision(begin);
            if (early is not null)
            {
                if (dbTx is not null)
                    await dbTx.RollbackAsync(ct);
                return (null, begin, early);
            }

            return (dbTx, begin, null);
        }
        catch
        {
            if (dbTx is not null)
                await dbTx.RollbackAsync(ct);
            throw;
        }
    }

    public static IResult ReplayStoredJson(string? resultJson, int successStatusCode)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return Results.StatusCode(successStatusCode);

        try
        {
            var node = JsonNode.Parse(resultJson);
            if (node is JsonObject obj)
            {
                if (obj.ContainsKey("alreadyProcessed"))
                    obj["alreadyProcessed"] = true;
                if (obj.ContainsKey("alreadyClaimed"))
                    obj["alreadyClaimed"] = true;
            }

            return Results.Json(node, statusCode: successStatusCode);
        }
        catch
        {
            return Results.Content(resultJson, "application/json", statusCode: successStatusCode);
        }
    }

    public static int MapErrorStatusCode(string? errorCode)
    {
        return errorCode switch
        {
            "insufficient_balance" => StatusCodes.Status409Conflict,
            "not_eligible" => StatusCodes.Status409Conflict,
            "unknown_reward" => StatusCodes.Status409Conflict,
            "invalid_reward_id" => StatusCodes.Status400BadRequest,
            "invalid_grant_id" => StatusCodes.Status409Conflict,
            "invalid_user_id" => StatusCodes.Status409Conflict,
            "invalid_season" => StatusCodes.Status409Conflict,
            "inactive_season" => StatusCodes.Status409Conflict,
            "invalid_reward_payload" => StatusCodes.Status409Conflict,
            "profile_not_found" => StatusCodes.Status409Conflict,
            "invalid_reward_type" => StatusCodes.Status400BadRequest,
            "unsupported_reward_type" => StatusCodes.Status409Conflict,
            "invalid_item" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
    }

    public static ApiErrorResponse BusinessError(string errorCode, string message)
        => new(false, errorCode, message);

    public static async Task<IDbContextTransaction?> BeginDbTransactionIfSupportedAsync(ApiDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsRelational())
            return null;

        return await db.Database.BeginTransactionAsync(ct);
    }

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
