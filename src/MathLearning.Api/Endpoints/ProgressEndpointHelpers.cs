using System.Text.Json;
using System.Text.Json.Nodes;
using MathLearning.Application.DTOs.Progress;
using MathLearning.Application.Services;

namespace MathLearning.Api.Endpoints;

internal static class ProgressEndpointHelpers
{
    public const string ProgressSyncEndpoint = "POST /api/progress/sync";
    public const string ProgressSyncOperationType = "progress_sync";
    public const string ProgressSyncRequiredVersion = "progress-sync-v2";

    public static bool TryResolveProgressSyncKeys(
        ProgressSyncRequestDto request,
        out string operationId,
        out string idempotencyKey)
    {
        operationId = string.Empty;
        idempotencyKey = string.Empty;

        var effectiveOperationId = TrimOrNull(request.OperationId) ?? TrimOrNull(request.IdempotencyKey);
        var effectiveIdempotencyKey = TrimOrNull(request.IdempotencyKey) ?? TrimOrNull(request.OperationId);

        if (string.IsNullOrWhiteSpace(effectiveOperationId) ||
            string.IsNullOrWhiteSpace(effectiveIdempotencyKey))
        {
            return false;
        }

        operationId = effectiveOperationId;
        idempotencyKey = effectiveIdempotencyKey;
        return true;
    }

    public static object BuildProgressSyncPayload(
        string deviceId,
        DateOnly day,
        IReadOnlyList<Guid> quizOperationIds,
        IReadOnlyList<Guid> practiceSessionIds)
    {
        return new
        {
            deviceId,
            day = day.ToString("yyyy-MM-dd"),
            quizOperationIds,
            practiceSessionIds
        };
    }

    public static IResult CompatibilityErrorResponse()
        => Results.Json(new ProgressSyncCompatibilityResponse(
            "progress_sync_legacy_client",
            "Legacy completed/day progress sync payloads are no longer accepted. Submit stable operation identity and settled evidence.",
            ProgressSyncRequiredVersion),
            statusCode: StatusCodes.Status426UpgradeRequired);

    public static IResult ConflictResult(string operationId, string idempotencyKey)
        => Results.Conflict(new ProgressSyncIdempotencyConflictResponse(
            AlreadyProcessed: false,
            Conflict: true,
            ErrorCode: "idempotency_conflict",
            OperationId: operationId,
            IdempotencyKey: idempotencyKey));

    public static IResult? HandleIdempotentDecision(IdempotencyLedgerBeginResult begin)
    {
        if (begin.ShouldProcess)
            return null;

        if (begin.IsCompleted || begin.IsFailed)
            return ReplayStoredJson(begin.ResultJson, begin.Ledger.HttpStatus);

        if (begin.IsPending)
        {
            return Results.Conflict(new
            {
                alreadyProcessed = false,
                conflict = false,
                errorCode = "transaction_in_progress",
                message = "A matching request is already being processed."
            });
        }

        return ConflictResult(begin.Ledger.OperationId, begin.Ledger.IdempotencyKey);
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
                obj["alreadyProcessed"] = true;
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
}
