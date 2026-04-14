using MathLearning.Api.Extensions;
using MathLearning.Application.DTOs.Common;
using MathLearning.Application.DTOs.Practice;
using MathLearning.Application.Services;

namespace MathLearning.Api.Endpoints;

public static class PracticeSessionEndpoints
{
    public static void MapPracticeSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/practice/session")
            .RequireAuthorization()
            .WithTags("Practice Session");

        group.MapPost("/start", async (
            StartPracticeSessionRequest request,
            IPracticeSessionService service,
            HttpContext ctx) =>
        {
            var userId = ResolveUserId(ctx);
            if (userId is null)
                return ApiResult<object>.Fail("Unauthorized", "UNAUTHORIZED").ToHttpResult(ctx);

            try
            {
                var result = await service.StartSessionAsync(userId, request, ctx.RequestAborted);
                return ApiResult<StartPracticeSessionResponse>.Ok(result).ToHttpResult(ctx);
            }
            catch (Exception ex)
            {
                return MapError<StartPracticeSessionResponse>(ex).ToHttpResult(ctx);
            }
        })
        .WithName("StartPracticeSession");

        group.MapPost("/{sessionId:guid}/answer", async (
            Guid sessionId,
            SubmitPracticeAnswerRequest request,
            IPracticeSessionService service,
            HttpContext ctx) =>
        {
            var userId = ResolveUserId(ctx);
            if (userId is null)
                return ApiResult<object>.Fail("Unauthorized", "UNAUTHORIZED").ToHttpResult(ctx);

            try
            {
                var result = await service.SubmitAnswerAsync(userId, sessionId, request, ctx.RequestAborted);
                return ApiResult<SubmitPracticeAnswerResponse>.Ok(result).ToHttpResult(ctx);
            }
            catch (Exception ex)
            {
                return MapError<SubmitPracticeAnswerResponse>(ex).ToHttpResult(ctx);
            }
        })
        .WithName("SubmitPracticeSessionAnswer");

        group.MapPost("/{sessionId:guid}/complete", async (
            Guid sessionId,
            IPracticeSessionService service,
            HttpContext ctx) =>
        {
            var userId = ResolveUserId(ctx);
            if (userId is null)
                return ApiResult<object>.Fail("Unauthorized", "UNAUTHORIZED").ToHttpResult(ctx);

            try
            {
                var result = await service.CompleteSessionAsync(userId, sessionId, ctx.RequestAborted);
                return ApiResult<CompletePracticeSessionResponse>.Ok(result).ToHttpResult(ctx);
            }
            catch (Exception ex)
            {
                return MapError<CompletePracticeSessionResponse>(ex).ToHttpResult(ctx);
            }
        })
        .WithName("CompletePracticeSession");
    }

    private static string? ResolveUserId(HttpContext ctx) =>
        ctx.User.FindFirst("userId")?.Value;

    private static ApiResult<T> MapError<T>(Exception ex)
    {
        return ex switch
        {
            ArgumentException => ApiResult<T>.Fail(ex.Message, "VALIDATION_ERROR"),
            KeyNotFoundException => ApiResult<T>.Fail(ex.Message, "NOT_FOUND"),
            InvalidOperationException => ApiResult<T>.Fail(ex.Message, "CONFLICT"),
            _ => ApiResult<T>.Fail("Practice session request failed.", "INTERNAL_ERROR")
        };
    }
}
