using System.Text.Json;
using System.Text.Json.Nodes;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;

namespace MathLearning.Api.Endpoints;

internal static class QuizEndpointHelpers
{
    public const string QuizAnswerEndpoint = "POST /api/quiz/answer";

    public static bool TryResolveQuizAnswerKeys(
        JsonElement request,
        out string operationId,
        out string idempotencyKey)
    {
        operationId = string.Empty;
        idempotencyKey = string.Empty;

        var rawOperationId = TryGetString(request, "operationId");
        var rawIdempotencyKey = TryGetString(request, "idempotencyKey");
        var effectiveOperationId = rawOperationId ?? rawIdempotencyKey;
        var effectiveIdempotencyKey = rawIdempotencyKey ?? rawOperationId;

        if (string.IsNullOrWhiteSpace(effectiveOperationId) ||
            string.IsNullOrWhiteSpace(effectiveIdempotencyKey))
        {
            return false;
        }

        operationId = effectiveOperationId.Trim();
        idempotencyKey = effectiveIdempotencyKey.Trim();
        return true;
    }

    public static object BuildQuizAnswerIdempotencyPayload(
        string quizId,
        int questionId,
        string answer,
        int timeSpentSeconds)
    {
        return new
        {
            quizId,
            questionId,
            answer,
            timeSpentSeconds
        };
    }

    public static async Task<(IdempotencyLedgerBeginResult? Begin, IResult? Error)> TryBeginQuizAnswerAsync(
        IIdempotencyLedgerService idempotencyService,
        string userId,
        string operationId,
        string idempotencyKey,
        object requestPayload,
        CancellationToken ct)
    {
        try
        {
            var begin = await idempotencyService.BeginOrGetExistingAsync(
                userId,
                QuizOperationTypes.QuizAnswer,
                operationId,
                idempotencyKey,
                QuizAnswerEndpoint,
                requestPayload,
                ct);
            return (begin, null);
        }
        catch (IdempotencyLedgerConflictException ex)
        {
            return (null, IdempotencyConflictResult(ex.OperationId, ex.IdempotencyKey));
        }
    }

    public static IResult? HandleIdempotentDecision(IdempotencyLedgerBeginResult begin)
    {
        if (begin.ShouldProcess)
            return null;

        if (begin.IsCompleted)
            return ReplayStoredJson(begin.ResultJson, begin.Ledger.HttpStatus);

        if (begin.IsFailed)
            return ReplayStoredJson(begin.ResultJson, begin.Ledger.HttpStatus);

        if (begin.IsPending)
        {
            return Results.Conflict(
                EconomyEndpointHelpers.BusinessError(
                    "transaction_in_progress",
                    "A matching request is already being processed."));
        }

        return IdempotencyConflictResult(begin.Ledger.OperationId, begin.Ledger.IdempotencyKey);
    }

    public static IResult IdempotencyConflictResult(string operationId, string idempotencyKey)
    {
        return Results.Conflict(new QuizIdempotencyConflictResponse(
            AlreadyProcessed: false,
            Conflict: true,
            ErrorCode: "idempotency_conflict",
            OperationId: operationId,
            IdempotencyKey: idempotencyKey));
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

    private static string? TryGetString(JsonElement json, string property)
    {
        if (!json.TryGetProperty(property, out var node))
            return null;
        if (node.ValueKind != JsonValueKind.String)
            return null;

        var value = node.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public sealed record QuizIdempotencyConflictResponse(
    bool AlreadyProcessed,
    bool Conflict,
    string ErrorCode,
    string OperationId,
    string IdempotencyKey);
