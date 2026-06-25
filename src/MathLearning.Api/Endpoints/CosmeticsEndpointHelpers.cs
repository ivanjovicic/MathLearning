using System.Text.Json.Nodes;
using MathLearning.Application.Services;

namespace MathLearning.Api.Endpoints;

internal static class CosmeticsEndpointHelpers
{
    public static bool TryResolveMutationKeys(
        string? operationId,
        string? idempotencyKey,
        string? transactionId,
        out string resolvedOperationId,
        out string resolvedIdempotencyKey,
        out IResult? error)
    {
        resolvedOperationId = string.Empty;
        resolvedIdempotencyKey = string.Empty;

        var effectiveIdempotencyKey = TrimOrNull(idempotencyKey) ?? TrimOrNull(transactionId);
        var effectiveOperationId = TrimOrNull(operationId) ?? TrimOrNull(transactionId);

        if (string.IsNullOrWhiteSpace(effectiveOperationId))
        {
            error = Results.BadRequest(
                EconomyEndpointHelpers.BusinessError("invalid_operation_id", "OperationId is required."));
            return false;
        }

        if (string.IsNullOrWhiteSpace(effectiveIdempotencyKey))
        {
            error = Results.BadRequest(
                EconomyEndpointHelpers.BusinessError("invalid_idempotency_key", "IdempotencyKey is required."));
            return false;
        }

        resolvedOperationId = effectiveOperationId;
        resolvedIdempotencyKey = effectiveIdempotencyKey;
        error = null;
        return true;
    }

    public static async Task<(CosmeticsIdempotencyBeginResult? Begin, IResult? Error)> TryBeginCosmeticsMutationAsync(
        ICosmeticsIdempotencyService idempotencyService,
        string userId,
        string operationType,
        string operationId,
        string idempotencyKey,
        object requestPayload,
        CancellationToken ct)
    {
        try
        {
            var begin = await idempotencyService.BeginOrGetExistingAsync(
                userId,
                operationType,
                operationId,
                idempotencyKey,
                requestPayload,
                ct);
            return (begin, null);
        }
        catch (CosmeticsIdempotencyConflictException)
        {
            return (null, Results.Conflict(new CosmeticsMutationConflictResponse(
                Success: false,
                AlreadyProcessed: false,
                AlreadyClaimed: false,
                Conflict: true,
                ErrorCode: "idempotency_conflict",
                Message: "Idempotency keys already exist with a different request payload.")));
        }
    }

    public static IResult? HandleCosmeticsIdempotentDecision(
        CosmeticsIdempotencyBeginResult begin,
        bool markAlreadyClaimed)
    {
        if (begin.ShouldProcess)
            return null;

        if (begin.IsCompleted)
        {
            return ReplayCosmeticsJson(
                begin.ResultJson,
                markAlreadyClaimed,
                successStatusCode: StatusCodes.Status200OK);
        }

        if (begin.IsFailed)
        {
            return ReplayCosmeticsJson(
                begin.ResultJson,
                markAlreadyClaimed,
                successStatusCode: EconomyEndpointHelpers.MapErrorStatusCode(begin.ErrorCode));
        }

        if (begin.IsPending)
        {
            return Results.Conflict(
                EconomyEndpointHelpers.BusinessError(
                    "transaction_in_progress",
                    "A matching request is already being processed."));
        }

        return Results.Conflict(new CosmeticsMutationConflictResponse(
            Success: false,
            AlreadyProcessed: false,
            AlreadyClaimed: false,
            Conflict: true,
            ErrorCode: "idempotency_conflict",
            Message: "Invalid transaction state."));
    }

    private static IResult ReplayCosmeticsJson(
        string? resultJson,
        bool markAlreadyClaimed,
        int successStatusCode)
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
                if (markAlreadyClaimed && obj.ContainsKey("alreadyClaimed"))
                    obj["alreadyClaimed"] = true;
                else if (markAlreadyClaimed)
                    obj["alreadyClaimed"] = true;
            }

            return Results.Json(node, statusCode: successStatusCode);
        }
        catch
        {
            return Results.Content(resultJson, "application/json", statusCode: successStatusCode);
        }
    }

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string ResolveSource(string? source, string? sourceType)
    {
        var raw = !string.IsNullOrWhiteSpace(source) ? source : sourceType;
        return EconomyEndpointHelpers.Normalize(raw);
    }

    public static string? NormalizeSourceEvent(string? sourceEvent)
        => TrimOrNull(sourceEvent);
}

public sealed record CosmeticsMutationConflictResponse(
    bool Success,
    bool AlreadyProcessed,
    bool AlreadyClaimed,
    bool Conflict,
    string ErrorCode,
    string Message
);
