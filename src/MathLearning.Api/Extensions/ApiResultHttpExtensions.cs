using MathLearning.Application.DTOs.Common;

namespace MathLearning.Api.Extensions;

public static class ApiResultHttpExtensions
{
    public static IResult ToHttpResult<T>(
        this ApiResult<T> result,
        HttpContext? context = null,
        int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsRateLimited && result.RetryAfterSeconds is > 0 && context is not null)
            context.Response.Headers.RetryAfter = result.RetryAfterSeconds.Value.ToString();

        var statusCode = result.Success
            ? successStatusCode
            : ResolveFailureStatusCode(result);

        return Results.Json(result, statusCode: statusCode);
    }

    private static int ResolveFailureStatusCode<T>(ApiResult<T> result)
    {
        if (result.IsRateLimited)
            return StatusCodes.Status429TooManyRequests;

        return result.ErrorCode switch
        {
            "VALIDATION_ERROR" => StatusCodes.Status400BadRequest,
            "UNAUTHORIZED" => StatusCodes.Status401Unauthorized,
            "FORBIDDEN" => StatusCodes.Status403Forbidden,
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "CONFLICT" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
