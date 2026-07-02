using System.Net;
using Microsoft.AspNetCore.Http;

namespace MathLearning.Api.Middleware;

/// <summary>
/// Captures the TCP peer IP before forwarded-headers middleware can rewrite <see cref="HttpContext.Connection.RemoteIpAddress"/>.
/// </summary>
public sealed class ConnectionRemoteIpMiddleware
{
    public const string ItemKey = "PhysicalRemoteIpAddress";

    private readonly RequestDelegate _next;

    public ConnectionRemoteIpMiddleware(RequestDelegate next) => _next = next;

    public Task Invoke(HttpContext context)
    {
        context.Items[ItemKey] = context.Connection.RemoteIpAddress;
        return _next(context);
    }
}
