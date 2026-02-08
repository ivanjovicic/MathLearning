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
        group.MapGet("/db", async (ApiDbContext db) =>
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
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    status = "Unhealthy",
                    db = "Error",
                    message = ex.Message,
                    timestamp = DateTime.UtcNow
                }, statusCode: 503);
            }
        })
        .WithName("DatabaseHealthCheck")
        .WithDescription("Check PostgreSQL database connectivity");

        // 📊 Detailed readiness check (DB + data counts)
        group.MapGet("/ready", async (ApiDbContext db) =>
        {
            try
            {
                var canConnect = await db.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return Results.Json(new
                    {
                        status = "NotReady",
                        reason = "Database not reachable"
                    }, statusCode: 503);
                }

                var questionCount = await db.Questions.CountAsync();
                var categoryCount = await db.Categories.CountAsync();
                var userCount = await db.UserProfiles.CountAsync();

                return Results.Ok(new
                {
                    status = "Ready",
                    db = "Connected",
                    data = new
                    {
                        questions = questionCount,
                        categories = categoryCount,
                        users = userCount
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    status = "NotReady",
                    reason = ex.Message
                }, statusCode: 503);
            }
        })
        .WithName("ReadinessCheck")
        .WithDescription("Full readiness check including database and seed data");
    }
}
