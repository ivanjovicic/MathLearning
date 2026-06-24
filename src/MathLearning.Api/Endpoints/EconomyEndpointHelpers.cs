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

    public static async Task<(EconomyTransactionBeginResult? Begin, IResult? Error)> TryBeginAsync(
        IEconomyTransactionService txService,
        string userId,
        string transactionType,
        string idempotencyKey,
        object requestPayload,
        CancellationToken ct)
    {
        try
        {
            var begin = await txService.BeginOrGetExistingAsync(
                userId,
                transactionType,
                idempotencyKey,
                requestPayload,
                cancellationToken: ct);
            return (begin, null);
        }
        catch (EconomyTransactionConflictException)
        {
            return (null, Results.Conflict(BusinessError("idempotency_conflict", "Idempotency key already exists with a different payload.")));
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
}
