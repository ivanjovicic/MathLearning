using MathLearning.Application.DTOs.Sync;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Sync;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var syncGroup = app.MapGroup("/api")
            .RequireAuthorization()
            .WithTags("Sync");

        syncGroup.MapPost("/devices/register", async (
            RegisterSyncDeviceRequest request,
            ISyncService syncService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var response = await syncService.RegisterDeviceAsync(userId, request, cancellationToken);
                return Results.Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        syncGroup.MapPost("/sync", async (
            SyncRequestDto request,
            ISyncService syncService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var response = await syncService.SyncAsync(userId, request, cancellationToken);
                return Results.Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        syncGroup.MapGet("/offline/bundle", async (
            IOfflineBundleService offlineBundleService,
            HttpContext ctx,
            CancellationToken cancellationToken,
            int? subtopicId,
            int questionCount = 100) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await offlineBundleService.GetBundleAsync(userId, subtopicId, questionCount, cancellationToken));
        });

        syncGroup.MapGet("/offline/bundle/manifest", async (
            IOfflineBundleService offlineBundleService,
            HttpContext ctx,
            CancellationToken cancellationToken,
            int? subtopicId,
            int questionCount = 100) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var bundle = await offlineBundleService.GetBundleAsync(userId, subtopicId, questionCount, cancellationToken);
            return Results.Ok(bundle.Manifest);
        });

        syncGroup.MapGet("/sync/checkpoint", async (
            string deviceId,
            ApiDbContext db,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var state = await db.DeviceSyncStates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.DeviceId == deviceId, cancellationToken);
            if (state is null)
            {
                return Results.NotFound(new { error = "Device sync state not found." });
            }

            var latestServerEvent = await db.ServerSyncEvents
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => (long?)x.Id)
                .MaxAsync(cancellationToken) ?? 0;

            return Results.Ok(new
            {
                deviceId = state.DeviceId,
                lastAcknowledgedEvent = state.LastAcknowledgedEvent,
                lastProcessedClientSequence = state.LastProcessedClientSequence,
                lastSyncTimeUtc = state.LastSyncTimeUtc,
                latestServerEvent
            });
        });

        syncGroup.MapGet("/sync/metrics", (SyncMetricsService metricsService) =>
            Results.Ok(metricsService.Snapshot()))
            .RequireAuthorization(DesignTokenSecurity.AdminPolicy);
    }
}
