namespace MathLearning.Api.Endpoints;

public static class EndpointUser
{
    public static string? GetUserId(HttpContext ctx) =>
        ctx.User.FindFirst("userId")?.Value;
}
