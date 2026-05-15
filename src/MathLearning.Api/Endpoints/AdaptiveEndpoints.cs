using MathLearning.Api.Extensions;
using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Common;
using MathLearning.Domain.Entities;
using System.Text.Json;

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
            HttpContext ctx) =>
        {
            // Compatibility: older mobile clients may send topicId/topic in request body.
            // We intentionally ignore this payload for now.
            // TODO: Topic-targeted adaptive sessions require backend product/design support.
            var userId = ResolveUserId(ctx);
            if (userId is null)
                return ApiResult<object>.Fail("Unauthorized", "UNAUTHORIZED").ToHttpResult(ctx);

            var result = await facade.StartAdaptiveSessionAsync(userId, ctx.RequestAborted);
            return result.ToHttpResult(ctx);
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

        var responseTimeSeconds = 0;
        if (TryReadInt(props, "responseTimeSeconds", out var parsedSeconds) && parsedSeconds >= 0)
        {
            responseTimeSeconds = parsedSeconds;
        }
        else if (TryReadInt(props, "responseTimeMs", out var parsedMs) && parsedMs >= 0)
        {
            responseTimeSeconds = (int)Math.Round(parsedMs / 1000d, MidpointRounding.AwayFromZero);
        }

        var confidence = 0.5d;
        if (TryReadDouble(props, "confidence", out var parsedConfidence))
            confidence = parsedConfidence;

        DateTime? answeredAt = null;
        if (TryReadDateTime(props, "answeredAt", out var parsedAnsweredAt))
            answeredAt = parsedAnsweredAt;

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
