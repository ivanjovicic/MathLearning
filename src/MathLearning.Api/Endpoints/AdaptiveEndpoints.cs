using MathLearning.Api.Extensions;
using MathLearning.Api.Services;
using MathLearning.Application.DTOs.Common;
using MathLearning.Domain.Entities;

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
            var userId = ResolveUserId(ctx);
            if (userId is null)
                return ApiResult<object>.Fail("Unauthorized", "UNAUTHORIZED").ToHttpResult(ctx);

            var result = await facade.StartAdaptiveSessionAsync(userId, ctx.RequestAborted);
            return result.ToHttpResult(ctx);
        })
        .WithName("StartAdaptiveSession");

        group.MapPost("/session/answer", async (
            AdaptiveAnswerRequest request,
            AdaptiveApiFacade facade,
            HttpContext ctx) =>
        {
            var userId = ResolveUserId(ctx);
            if (userId is null)
                return ApiResult<object>.Fail("Unauthorized", "UNAUTHORIZED").ToHttpResult(ctx);

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
            var userId = ResolveUserId(ctx);
            if (userId is null)
                return ApiResult<object>.Fail("Unauthorized", "UNAUTHORIZED").ToHttpResult(ctx);

            var result = await facade.GetDueReviewsAsync(userId, ctx.RequestAborted);
            return result.ToHttpResult(ctx);
        })
        .WithName("GetDueAdaptiveReviews");
    }

    private static string? ResolveUserId(HttpContext ctx) =>
        ctx.User.FindFirst("userId")?.Value;
}
