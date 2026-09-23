using MathLearning.Api.Middleware;
using MathLearning.Api.Services;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

using MathLearning.Api.Startup;

namespace MathLearning.Api.Endpoints;

public static class HealthEndpoints
{
    internal static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    internal static async Task ExecuteBoundedProbeAsync(
        Func<CancellationToken, Task> probe,
        CancellationToken requestAborted)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        timeout.CancelAfter(ProbeTimeout);
        await probe(timeout.Token);
    }

    internal static async Task<T> ExecuteBoundedProbeAsync<T>(
        Func<CancellationToken, Task<T>> probe,
        CancellationToken requestAborted)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        timeout.CancelAfter(ProbeTimeout);
        return await probe(timeout.Token);
    }

    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health")
                       .WithTags("Health")
                       .AllowAnonymous();

        // Basic liveness check intentionally has no database or other external dependency.
        group.MapGet("/", () => Results.Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow
        }))
        .WithName("HealthCheck")
        .WithDescription("Basic liveness check");

        group.MapGet("/db", async (
            ApiDbContext db,
            DatabaseSchemaState schemaState,
            RedisRuntimeStatus redisStatus,
            ILogger<global::Program> logger,
            HttpContext httpContext) =>
        {
            var isRelational = db.Database.IsRelational();
            var previousCommandTimeout = isRelational ? db.Database.GetCommandTimeout() : null;
            try
            {
                if (isRelational)
                    db.Database.SetCommandTimeout((int)ProbeTimeout.TotalSeconds);
                return await ExecuteBoundedProbeAsync(async cancellationToken =>
                {
                    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
                    if (!canConnect)
                    {
                        return Results.Json(new
                        {
                            status = "Unhealthy",
                            reason = "DatabaseUnavailable",
                            timestamp = DateTime.UtcNow
                        }, statusCode: 503);
                    }

                    await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

                    return Results.Ok(new
                    {
                        status = "Healthy",
                        timestamp = DateTime.UtcNow
                    });
                }, httpContext.RequestAborted);
            }
            catch (Exception exception)
            {
                LogProbeFailure(logger, httpContext, "db", exception, "DatabaseHealthCheckFailed");
                return Results.Json(new
                {
                    status = "Unhealthy",
                    reason = GetFailureReason(exception, "DatabaseHealthCheckFailed"),
                    timestamp = DateTime.UtcNow
                }, statusCode: 503);
            }
            finally
            {
                if (isRelational)
                    db.Database.SetCommandTimeout(previousCommandTimeout);
            }
        })
        .WithName("DatabaseHealthCheck")
        .WithDescription("Check PostgreSQL database connectivity");

        group.MapGet("/ready", async (
            ApiDbContext db,
            DatabaseSchemaState schemaState,
            ICosmeticCatalogService catalogService,
            RedisRuntimeStatus redisStatus,
            ILogger<global::Program> logger,
            HttpContext httpContext) =>
        {
            var isRelational = db.Database.IsRelational();
            var previousCommandTimeout = isRelational ? db.Database.GetCommandTimeout() : null;
            try
            {
                if (isRelational)
                    db.Database.SetCommandTimeout((int)ProbeTimeout.TotalSeconds);
                return await ExecuteBoundedProbeAsync(async cancellationToken =>
                {
                    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
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

                    var catalogReadiness = await catalogService.GetCatalogReadinessAsync(cancellationToken);
                    if (!catalogReadiness.IsReady)
                    {
                        return Results.Json(new
                        {
                            status = catalogReadiness.Status,
                            reason = catalogReadiness.Reason,
                            timestamp = DateTime.UtcNow
                        }, statusCode: 503);
                    }

                    var redisReadiness = redisStatus.Snapshot();
                    if (redisReadiness.Required && !redisReadiness.Connected)
                    {
                        return Results.Json(new
                        {
                            status = "NotReady",
                            reason = "RedisUnavailable",
                            timestamp = DateTime.UtcNow
                        }, statusCode: 503);
                    }

                    return Results.Ok(new
                    {
                        status = "Ready",
                        timestamp = DateTime.UtcNow
                    });
                }, httpContext.RequestAborted);
            }
            catch (Exception exception)
            {
                LogProbeFailure(logger, httpContext, "ready", exception, "ReadinessCheckFailed");
                return Results.Json(new
                {
                    status = "NotReady",
                    reason = GetFailureReason(exception, "ReadinessCheckFailed"),
                    timestamp = DateTime.UtcNow
                }, statusCode: 503);
            }
            finally
            {
                if (isRelational)
                    db.Database.SetCommandTimeout(previousCommandTimeout);
            }
        })
        .WithName("ReadinessCheck")
        .WithDescription("Full readiness check including database and seed data");

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

    private static void LogProbeFailure(
        ILogger logger,
        HttpContext httpContext,
        string probe,
        Exception exception,
        string reason)
    {
        logger.LogWarning(
            "Health probe failed. Probe={Probe} Reason={Reason} ExceptionType={ExceptionType} CorrelationId={CorrelationId} TraceId={TraceId}",
            probe,
            reason,
            exception.GetType().FullName,
            SafeClientErrorResponse.ResolveCorrelationId(httpContext) ?? "unknown",
            httpContext.TraceIdentifier);
    }

    private static string GetFailureReason(Exception exception, string fallback) =>
        exception is OperationCanceledException ? "HealthProbeTimeout" : fallback;

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
