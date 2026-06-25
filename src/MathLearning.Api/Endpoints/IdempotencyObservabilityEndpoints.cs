using MathLearning.Infrastructure.Services.Idempotency;

namespace MathLearning.Api.Endpoints;

public static class IdempotencyObservabilityEndpoints
{
    public static void MapIdempotencyObservabilityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/monitoring/idempotency")
            .WithTags("Monitoring")
            .RequireAuthorization(DesignTokenSecurity.AdminPolicy);

        group.MapGet("", (IdempotencyObservabilityService observability) =>
            Results.Ok(observability.Snapshot()))
            .WithName("GetIdempotencyObservabilitySnapshot")
            .WithSummary("Returns in-memory counters for idempotency replay/conflict/failure visibility.");
    }
}
