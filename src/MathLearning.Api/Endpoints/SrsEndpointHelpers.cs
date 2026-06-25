using MathLearning.Application.Services;
using MathLearning.Domain.Entities;

namespace MathLearning.Api.Endpoints;

internal static class SrsEndpointHelpers
{
    public const string SrsUpdateEndpoint = "POST /api/quiz/srs/update";

    public static bool TryResolveSrsUpdateKeys(
        string? rawOperationId,
        string? rawIdempotencyKey,
        out string operationId,
        out string idempotencyKey)
    {
        operationId = string.Empty;
        idempotencyKey = string.Empty;

        var effectiveOperationId = TrimOrNull(rawOperationId) ?? TrimOrNull(rawIdempotencyKey);
        var effectiveIdempotencyKey = TrimOrNull(rawIdempotencyKey) ?? TrimOrNull(rawOperationId);

        if (string.IsNullOrWhiteSpace(effectiveOperationId) ||
            string.IsNullOrWhiteSpace(effectiveIdempotencyKey))
        {
            return false;
        }

        operationId = effectiveOperationId;
        idempotencyKey = effectiveIdempotencyKey;
        return true;
    }

    public static object BuildSrsUpdateIdempotencyPayload(int questionId, bool isCorrect, int timeMs)
    {
        return new
        {
            questionId,
            isCorrect,
            timeMs
        };
    }

    public static object BuildSrsUpdateResponse(int questionId, DateTime nextReview, int streak, double ease)
    {
        return new
        {
            questionId,
            nextReview,
            streak,
            ease
        };
    }

    public static async Task<(IdempotencyLedgerBeginResult? Begin, IResult? Error)> TryBeginSrsUpdateAsync(
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
                QuizOperationTypes.SrsUpdate,
                operationId,
                idempotencyKey,
                SrsUpdateEndpoint,
                requestPayload,
                ct);
            return (begin, null);
        }
        catch (IdempotencyLedgerConflictException ex)
        {
            return (null, QuizEndpointHelpers.IdempotencyConflictResult(ex.OperationId, ex.IdempotencyKey));
        }
    }

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
