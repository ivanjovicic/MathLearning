using MathLearning.Application.DTOs.Common;
using MathLearning.Infrastructure.Services.Sync;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace MathLearning.Api.Middleware;

public sealed class SyncRequestBodySizeLimitMiddleware
{
    private static readonly PathString[] TargetPaths =
    [
        new PathString("/api/sync"),
        new PathString("/api/devices/register")
    ];

    private readonly RequestDelegate next;
    private readonly IOptions<SyncOptions> options;

    public SyncRequestBodySizeLimitMiddleware(RequestDelegate next, IOptions<SyncOptions> options)
    {
        this.next = next;
        this.options = options;
    }

    public async Task Invoke(HttpContext context)
    {
        if (context.Request.Method.Equals(HttpMethods.Post, StringComparison.OrdinalIgnoreCase) &&
            IsTargetPath(context.Request.Path))
        {
            var maxBytes = Math.Max(1, options.Value.MaxRequestBodyBytes);
            var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is not null && !feature.IsReadOnly)
            {
                feature.MaxRequestBodySize = maxBytes;
            }

            if (context.Request.ContentLength is > 0 && context.Request.ContentLength > maxBytes)
            {
                var traceId = SafeClientErrorResponse.ResolveTraceId(context);
                var correlationId = SafeClientErrorResponse.ResolveCorrelationId(context);
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(ApiResult<object>.Fail(
                    "Request body is too large.",
                    "request_too_large",
                    new { traceId, correlationId },
                    traceId));
                return;
            }
        }

        await next(context);
    }

    private static bool IsTargetPath(PathString path) =>
        TargetPaths.Any(target => path.StartsWithSegments(target, StringComparison.OrdinalIgnoreCase));
}
