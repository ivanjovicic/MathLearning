using MathLearning.Api.Extensions;
using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Common;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MathLearning.Api.Endpoints;

public static class AdaptiveEndpoints
{
    public static void MapAdaptiveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/adaptive")
            .RequireAuthorization()
            .WithTags("Adaptive");

        group.MapPost("/session/start", async (
            AdaptiveApiFacade facade,
            ApiDbContext db,
            IIdempotencyLedgerService idempotencyService,
            HttpRequest request,
            HttpContext ctx) =>
        {
            var userId = ResolveUserId(ctx);
            if (userId is null)
                return ApiResult<object>.Fail("Unauthorized", "UNAUTHORIZED").ToHttpResult(ctx);

            var (requestPayload, operationId, idempotencyKey, parseError) = await TryReadAdaptiveSessionStartRequestAsync(
                request,
                ctx.RequestAborted);

            if (parseError is not null)
                return ApiResult<object>.Fail(parseError, "VALIDATION_ERROR").ToHttpResult(ctx);

            return await facade.StartAdaptiveSessionAsync(
                userId,
                db,
                idempotencyService,
                requestPayload,
                operationId,
                idempotencyKey,
                ctx.RequestAborted);
        })
        .WithName("StartAdaptiveSession");

        group.MapPost("/session/answer", async (
            JsonElement requestPayload,
            AdaptiveApiFacade facade,
            HttpContext ctx) =>
        {
            var userId = ResolveUserId(ctx);
            if (userId is null)
                return ApiResult<object>.Fail("Unauthorized", "UNAUTHORIZED").ToHttpResult(ctx);

            if (!TryBuildAdaptiveAnswerRequest(requestPayload, out var request, out var parseError))
            {
                return ApiResult<object>.Fail(parseError!, "VALIDATION_ERROR").ToHttpResult(ctx);
            }

            var result = await facade.SubmitAdaptiveSessionAnswerAsync(userId, request, ctx.RequestAborted);
            return result.ToHttpResult(ctx);
        })
        .WithName("SubmitAdaptiveSessionAnswer");

        group.MapGet("/path", async (
            AdaptiveApiFacade facade,
            HttpContext ctx) =>
        {
            var userId = ResolveUserId(ctx);
            if (userId is null)
                return ApiResult<object>.Fail("Unauthorized", "UNAUTHORIZED").ToHttpResult(ctx);

            var result = await facade.GetAdaptivePathAsync(userId, ctx.RequestAborted);
            return result.ToHttpResult(ctx);
        })
        .WithName("GetAdaptivePath");

        group.MapGet("/recommendations", async (
            AdaptiveApiFacade facade,
            HttpContext ctx) =>
        {
            var userId = ResolveUserId(ctx);
            if (userId is null)
                return ApiResult<object>.Fail("Unauthorized", "UNAUTHORIZED").ToHttpResult(ctx);

            var result = await facade.GetAdaptiveRecommendationsAsync(userId, ctx.RequestAborted);
            return result.ToHttpResult(ctx);
        })
        .WithName("GetAdaptiveRecommendations");

        group.MapGet("/reviews/due", async (
            AdaptiveApiFacade facade,
            HttpContext ctx) =>
        {
            return await GetDueReviewsResultAsync(facade, ctx);
        })
        .WithName("GetDueAdaptiveReviews");

        // Mobile compatibility alias. Canonical route remains: GET /api/adaptive/reviews/due
        group.MapGet("/review", async (
            AdaptiveApiFacade facade,
            HttpContext ctx) =>
        {
            return await GetDueReviewsResultAsync(facade, ctx);
        })
        .WithName("GetDueAdaptiveReviewsCompatibilityAlias")
        .WithSummary("Mobile compatibility alias for GET /api/adaptive/reviews/due");
    }

    private static string? ResolveUserId(HttpContext ctx) =>
        ctx.User.FindFirst("userId")?.Value;

    private static async Task<IResult> GetDueReviewsResultAsync(AdaptiveApiFacade facade, HttpContext ctx)
    {
        var userId = ResolveUserId(ctx);
        if (userId is null)
            return ApiResult<object>.Fail("Unauthorized", "UNAUTHORIZED").ToHttpResult(ctx);

        var result = await facade.GetDueReviewsAsync(userId, ctx.RequestAborted);
        return result.ToHttpResult(ctx);
    }

    private static async Task<(JsonObject? RequestPayload, string? OperationId, string? IdempotencyKey, string? Error)> TryReadAdaptiveSessionStartRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is 0)
            return (null, null, null, null);

        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var requestBody = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(requestBody))
            return (null, null, null, null);

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(requestBody);
        }
        catch (JsonException)
        {
            return (null, null, null, "Request body must be valid JSON.");
        }

        if (node is not JsonObject payload)
            return (null, null, null, "Request body must be a JSON object.");

        var operationId = TryReadString(payload, "operationId");
        var idempotencyKey = TryReadString(payload, "idempotencyKey");
        var normalizedPayload = (JsonObject)payload.DeepClone();
        normalizedPayload.Remove("operationId");
        normalizedPayload.Remove("idempotencyKey");

        return (normalizedPayload, operationId, idempotencyKey, null);
    }

    private static bool TryBuildAdaptiveAnswerRequest(
        JsonElement requestPayload,
        out AdaptiveAnswerRequest request,
        out string? error)
    {
        request = new AdaptiveAnswerRequest();
        error = null;

        if (requestPayload.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || requestPayload.ValueKind != JsonValueKind.Object)
        {
            error = "Request body must be a JSON object.";
            return false;
        }

        var props = requestPayload.EnumerateObject()
            .ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);

        if (!TryReadGuid(props, "adaptiveSessionId", out var adaptiveSessionId)
            && !TryReadGuid(props, "sessionId", out adaptiveSessionId))
        {
            error = "AdaptiveSessionId is required (or sessionId for legacy clients).";
            return false;
        }

        if (!TryReadGuid(props, "adaptiveSessionItemId", out var adaptiveSessionItemId))
        {
            error = "AdaptiveSessionItemId is required.";
            return false;
        }

        if (!TryReadInt(props, "questionId", out var questionId) || questionId <= 0)
        {
            error = "QuestionId must be a positive integer.";
            return false;
        }

        if (!TryReadString(props, "answer", out var answer) || string.IsNullOrWhiteSpace(answer))
        {
            error = "Answer is required.";
            return false;
        }

        var confidence = 0.5d;
        if (props.TryGetValue("confidence", out var confidenceElement))
        {
            if (!TryReadDouble(confidenceElement, out var parsedConfidence))
            {
                error = "Confidence must be a finite number between 0 and 1.";
                return false;
            }

            confidence = parsedConfidence;
        }

        var responseTimeSeconds = 0;
        if (props.TryGetValue("responseTimeSeconds", out var responseTimeSecondsElement))
        {
            if (!TryReadInt(responseTimeSecondsElement, out var parsedSeconds))
            {
                error = "ResponseTimeSeconds must be a non-negative integer.";
                return false;
            }

            if (!AdaptiveAnswerInputBounds.TryValidateResponseTimeSeconds(parsedSeconds, out error))
                return false;

            responseTimeSeconds = parsedSeconds;
        }
        else if (props.TryGetValue("responseTimeMs", out var responseTimeMsElement))
        {
            if (!TryReadInt(responseTimeMsElement, out var parsedMs))
            {
                error = "ResponseTimeMs must be a non-negative integer.";
                return false;
            }

            if (!AdaptiveAnswerInputBounds.TryValidateResponseTimeMilliseconds(parsedMs, out error))
                return false;

            responseTimeSeconds = (int)Math.Round(parsedMs / 1000d, MidpointRounding.AwayFromZero);
            if (!AdaptiveAnswerInputBounds.TryValidateResponseTimeSeconds(responseTimeSeconds, out error))
                return false;
        }

        DateTime? answeredAt = null;
        if (props.TryGetValue("answeredAt", out var answeredAtElement))
        {
            if (answeredAtElement.ValueKind != JsonValueKind.String
                || !DateTime.TryParse(answeredAtElement.GetString(), out var parsedAnsweredAt))
            {
                error = "answeredAt is malformed.";
                return false;
            }

            if (!AdaptiveAnswerInputBounds.TryValidateAnsweredAt(parsedAnsweredAt, DateTime.UtcNow, out error))
                return false;

            answeredAt = OfflineAnswerTimestampPolicy.NormalizeToUtcMilliseconds(parsedAnsweredAt);
        }

        if (!AdaptiveAnswerInputBounds.TryValidateAnswer(answer, out error))
            return false;

        if (!AdaptiveAnswerInputBounds.TryValidateConfidence(confidence, out error))
            return false;

        request = new AdaptiveAnswerRequest
        {
            AdaptiveSessionId = adaptiveSessionId,
            AdaptiveSessionItemId = adaptiveSessionItemId,
            QuestionId = questionId,
            Answer = answer,
            ResponseTimeSeconds = responseTimeSeconds,
            Confidence = confidence,
            AnsweredAt = answeredAt
        };

        return true;
    }

    private static bool TryReadGuid(IReadOnlyDictionary<string, JsonElement> props, string key, out Guid value)
    {
        value = Guid.Empty;
        if (!props.TryGetValue(key, out var element))
            return false;

        if (element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out value))
            return true;

        return false;
    }

    private static bool TryReadInt(IReadOnlyDictionary<string, JsonElement> props, string key, out int value)
    {
        value = 0;
        if (!props.TryGetValue(key, out var element))
            return false;

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
            return true;

        if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out value))
            return true;

        return false;
    }

    private static bool TryReadDouble(JsonElement element, out double value)
    {
        value = 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value))
            return true;

        if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), out value))
            return true;

        return false;
    }

    private static bool TryReadInt(JsonElement element, out int value)
    {
        value = 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
            return true;

        if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out value))
            return true;

        return false;
    }

    private static bool TryReadDouble(IReadOnlyDictionary<string, JsonElement> props, string key, out double value)
    {
        value = 0;
        if (!props.TryGetValue(key, out var element))
            return false;

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value))
            return true;

        if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), out value))
            return true;

        return false;
    }

    private static bool TryReadString(IReadOnlyDictionary<string, JsonElement> props, string key, out string value)
    {
        value = string.Empty;
        if (!props.TryGetValue(key, out var element))
            return false;

        if (element.ValueKind != JsonValueKind.String)
            return false;

        var raw = element.GetString();
        if (raw is null)
            return false;

        value = raw;
        return true;
    }

    private static string? TryReadString(JsonObject props, string key)
    {
        if (!props.TryGetPropertyValue(key, out var node) || node is null)
            return null;

        if (node is not JsonValue valueNode || !valueNode.TryGetValue<string>(out var raw))
            return null;

        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    private static bool TryReadDateTime(IReadOnlyDictionary<string, JsonElement> props, string key, out DateTime value)
    {
        value = default;
        if (!props.TryGetValue(key, out var element))
            return false;

        if (element.ValueKind == JsonValueKind.String && DateTime.TryParse(element.GetString(), out value))
            return true;

        return false;
    }
}
