namespace MathLearning.Api.Endpoints;

public static class PowerupEndpoints
{
    public static void MapPowerupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/powerups")
            .RequireAuthorization()
            .WithTags("Powerups");

        group.MapPost("/streak-freeze/buy", (HttpContext ctx) =>
        {
            ctx.Response.Headers.Append("Sunset", "2026-10-01");
            return Results.Json(new
            {
                success = false,
                errorCode = "legacy_route_removed",
                message = "Use canonical purchase settlement under /api/shop/streak-freeze/purchase.",
                replacementRoute = "/api/shop/streak-freeze/purchase",
                removalDate = "2026-10-01"
            }, statusCode: StatusCodes.Status410Gone);
        })
        .WithName("BuyStreakFreeze");
    }
}
