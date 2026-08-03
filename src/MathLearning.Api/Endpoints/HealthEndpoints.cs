using MathLearning.Api.Services;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health")
                       .WithTags("Health")
                       .AllowAnonymous();

        // Basic liveness check — stable public probe fields only.
        group.MapGet("/", () => Results.Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow
        }))
        .WithName("HealthCheck")
        .WithDescription("Basic liveness check");

        // Database connectivity — public status + safe reason codes only.
        group.MapGet("/db", async (ApiDbContext db, DatabaseSchemaState schemaState) =>
        {
            try
            {
                var canConnect = await db.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return Results.Json(new
                    {
                        status = "Unhealthy",
                        reason = "DatabaseUnavailable",
                        timestamp = DateTime.UtcNow
                    }, statusCode: 503);
                }

                await db.Database.ExecuteSqlRawAsync("SELECT 1");

                if (!schemaState.Current.IsSchemaReady)
                {
                    return Results.Json(new
                    {
                        status = "Unhealthy",
                        reason = "SchemaNotReady",
                        timestamp = DateTime.UtcNow
                    }, statusCode: 503);
                }

                return Results.Ok(new
                {
                    status = "Healthy",
                    timestamp = DateTime.UtcNow
                });
            }
            catch
            {
                return Results.Json(new
                {
                    status = "Unhealthy",
                    reason = "DatabaseHealthCheckFailed",
                    timestamp = DateTime.UtcNow
                }, statusCode: 503);
            }
        })
        .WithName("DatabaseHealthCheck")
        .WithDescription("Check PostgreSQL database connectivity");

        // Readiness — public status + safe reason codes; no counts/checksums/migration names.
        group.MapGet("/ready", async (ApiDbContext db, DatabaseSchemaState schemaState, ICosmeticCatalogService catalogService) =>
        {
            try
            {
                var canConnect = await db.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return Results.Json(new
                    {
                        status = "NotReady",
                        reason = "DatabaseUnavailable",
                        timestamp = DateTime.UtcNow
                    }, statusCode: 503);
                }

                var schemaStatus = schemaState.Current;
                if (!schemaStatus.IsSchemaReady)
                {
                    return Results.Json(new
                    {
                        status = "NotReady",
                        reason = "SchemaNotReady",
                        timestamp = DateTime.UtcNow
                    }, statusCode: 503);
                }

                var catalogReadiness = await catalogService.GetCatalogReadinessAsync(CancellationToken.None);
                if (!catalogReadiness.IsReady)
                {
                    return Results.Json(new
                    {
                        status = catalogReadiness.Status,
                        reason = catalogReadiness.Reason,
                        timestamp = DateTime.UtcNow
                    }, statusCode: 503);
                }

                return Results.Ok(new
                {
                    status = "Ready",
                    timestamp = DateTime.UtcNow
                });
            }
            catch
            {
                return Results.Json(new
                {
                    status = "NotReady",
                    reason = "ReadinessCheckFailed",
                    timestamp = DateTime.UtcNow
                }, statusCode: 503);
            }
        })
        .WithName("ReadinessCheck")
        .WithDescription("Full readiness check including database and seed data");

        // Schema diagnostics are admin-only and intentionally outside the anonymous /api/health group.
        app.MapGet("/api/health/schema", BuildSchemaHealthResult)
            .RequireAuthorization(DesignTokenSecurity.AdminPolicy)
            .WithName("SchemaHealthCheck")
            .WithTags("Health")
            .WithDescription("Admin-only database schema/migration state");

        app.MapGet("/health/schema", BuildSchemaHealthResult)
            .RequireAuthorization(DesignTokenSecurity.AdminPolicy)
            .WithName("CanonicalSchemaHealthCheck")
            .WithTags("Health")
            .WithDescription("Admin-only database schema/migration state");
    }

    private static IResult BuildSchemaHealthResult(DatabaseSchemaState schemaState)
    {
        var schemaStatus = schemaState.Current;
        var payload = new
        {
            status = schemaStatus.Status,
            isSchemaReady = schemaStatus.IsSchemaReady,
            latestCodeMigration = schemaStatus.LatestCodeMigration,
            latestAppliedMigration = schemaStatus.LatestAppliedMigration,
            pendingMigrationsCount = schemaStatus.PendingMigrationsCount,
            unknownAppliedMigrationsCount = schemaStatus.UnknownAppliedMigrationsCount,
            failureMessage = schemaStatus.FailureMessage,
            checkedAtUtc = schemaStatus.CheckedAtUtc
        };

        return schemaStatus.IsSchemaReady
            ? Results.Ok(payload)
            : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
