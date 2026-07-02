using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace MathLearning.Api.Middleware;

public static class RateLimitClientIdentity
{
    public static string Resolve(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value
                ?? context.User.FindFirst("userId")?.Value;

            if (!string.IsNullOrWhiteSpace(userId))
                return $"user:{userId}";
        }

        var physicalIp = context.Items[ConnectionRemoteIpMiddleware.ItemKey] as IPAddress
            ?? context.Connection.RemoteIpAddress;

        return $"ip:{physicalIp?.ToString() ?? "unknown"}";
    }
}
