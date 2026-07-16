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

        // 🏥 Basic liveness check
        group.MapGet("/", () => Results.Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow
        }))
        .WithName("HealthCheck")
        .WithDescription("Basic liveness check");

        // 🗄️ Database connectivity check
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
                        db = "Cannot connect",
                        schema = BuildSchemaSummary(schemaState.Current),
                        timestamp = DateTime.UtcNow
                    }, statusCode: 503);
                }

                // Run a simple query to verify the connection is truly working
                await db.Database.ExecuteSqlRawAsync("SELECT 1");

                return Results.Ok(new
                {
                    status = "Healthy",
                    db = "Connected",
                    provider = "PostgreSQL",
                    schema = BuildSchemaSummary(schemaState.Current),
                    timestamp = DateTime.UtcNow
                });
            }
            catch
            {
                return Results.Json(new
                {
                    status = "Unhealthy",
                    db = "Error",
                    reason = "DatabaseHealthCheckFailed",
                    schema = BuildSchemaSummary(schemaState.Current),
                    timestamp = DateTime.UtcNow
                }, statusCode: 503);
            }
        })
        .WithName("DatabaseHealthCheck")
        .WithDescription("Check PostgreSQL database connectivity");

        // 📊 Detailed readiness check (DB + data counts)
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
                        schema = BuildSchemaSummary(schemaState.Current)
                    }, statusCode: 503);
                }

                var schemaStatus = schemaState.Current;
                if (!schemaStatus.IsSchemaReady)
                {
                    return Results.Json(new
                    {
                        status = "NotReady",
                        reason = "SchemaNotReady",
                        schema = BuildSchemaSummary(schemaStatus)
                    }, statusCode: 503);
                }

                var catalogReadiness = await catalogService.GetCatalogReadinessAsync(CancellationToken.None);
                if (!catalogReadiness.IsReady)
                {
                    return Results.Json(new
                    {
                        status = catalogReadiness.Status,
                        reason = catalogReadiness.Reason,
                        catalog = catalogReadiness,
                        schema = BuildSchemaSummary(schemaStatus)
                    }, statusCode: 503);
                }

                var questionCount = await db.Questions.CountAsync();
                var categoryCount = await db.Categories.CountAsync();
                var userCount = await db.UserProfiles.CountAsync();

                return Results.Ok(new
                {
                    status = "Ready",
                    db = "Connected",
                    catalog = new
                    {
                        catalogReadiness.Status,
                        catalogReadiness.RevisionKey,
                        catalogReadiness.Checksum,
                        catalogReadiness.CatalogVersion
                    },
                    data = new
                    {
                        questions = questionCount,
                        categories = categoryCount,
                        users = userCount
                    },
                    schema = BuildSchemaSummary(schemaStatus),
                    timestamp = DateTime.UtcNow
                });
            }
            catch
            {
                return Results.Json(new
                {
                    status = "NotReady",
                    reason = "ReadinessCheckFailed",
                    schema = BuildSchemaSummary(schemaState.Current)
                }, statusCode: 503);
            }
        })
        .WithName("ReadinessCheck")
        .WithDescription("Full readiness check including database and seed data");

        group.MapGet("/schema", BuildSchemaHealthResult)
        .WithName("SchemaHealthCheck")
        .WithDescription("Expose database schema/migration state");

        app.MapGet("/health/schema", BuildSchemaHealthResult)
            .AllowAnonymous()
            .WithName("CanonicalSchemaHealthCheck")
            .WithTags("Health")
            .WithDescription("Expose database schema/migration state");
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

    private static object BuildSchemaSummary(DatabaseSchemaStatus status)
    {
        return new
        {
            state = status.Status,
            isSchemaReady = status.IsSchemaReady,
            schemaVersion = status.LatestAppliedMigration,
            latestRequiredMigration = status.LatestCodeMigration,
            pendingMigrationsCount = status.PendingMigrationsCount,
            unknownAppliedMigrationsCount = status.UnknownAppliedMigrationsCount,
            checkedAtUtc = status.CheckedAtUtc,
            failure = status.FailureMessage
        };
    }
}
