using MathLearning.Application.DTOs.Bugs;
using MathLearning.Application.Services;
using Microsoft.AspNetCore.Authorization;

namespace MathLearning.Api.Endpoints;

public static class BugEndpoints
{
    public static void MapBugEndpoints(this IEndpointRouteBuilder app)
    {
        var publicGroup = app.MapGroup("/api/bugs")
                            .AllowAnonymous(); // Allow anonymous bug reports

        var adminGroup = app.MapGroup("/api/bugs")
                           .RequireAuthorization()
                           .WithTags("BugReports");

        // ==========================================
        // PUBLIC ENDPOINTS
        // ==========================================

        // POST /api/bugs/report - Report a bug (public)
        publicGroup.MapPost("/report", async (
            BugReportRequest request,
            IBugReportService bugService,
            HttpContext ctx) =>
        {
            // Try to get user ID if authenticated
            int? userId = null;
            string? userIdClaim = ctx.User.FindFirst("userId")?.Value;
            if (int.TryParse(userIdClaim, out int parsedUserId))
            {
                userId = parsedUserId;
            }

            // For anonymous reports, use a default user ID or create a temporary one
            if (!userId.HasValue)
            {
                // For now, require authentication for bug reports
                return Results.Unauthorized();
            }

            var result = await bugService.CreateBugReportAsync(userId.Value, request);
            return Results.Created($"/api/bugs/{result.Id}", result);
        })
        .WithName("ReportBug")
        .WithDescription("Report a bug from the frontend");

        // GET /api/bugs/mine - Get my bug reports (authenticated user)
        publicGroup.MapGet("/mine", async (
            IBugReportService bugService,
            HttpContext ctx,
            int page = 1,
            int pageSize = 50) =>
        {
            string? userIdClaim = ctx.User.FindFirst("userId")?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Results.Unauthorized();
            }

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var result = await bugService.GetMyBugReportsAsync(userId, page, pageSize);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetMyBugReports")
        .WithDescription("Get my submitted bug reports");

        // ==========================================
        // ADMIN ENDPOINTS
        // ==========================================

        // GET /api/bugs - List bug reports (admin)
        adminGroup.MapGet("/", async (
            IBugReportService bugService,
            int page = 1,
            int pageSize = 20,
            string? status = null,
            string? severity = null) =>
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var result = await bugService.GetBugReportsAsync(page, pageSize, status, severity);
            return Results.Ok(result);
        })
        .WithName("GetBugReports")
        .WithDescription("Get paginated list of bug reports");

        // GET /api/bugs/{id} - Get single bug report
        adminGroup.MapGet("/{id:guid}", async (
            Guid id,
            IBugReportService bugService) =>
        {
            var bug = await bugService.GetBugReportAsync(id);
            return bug != null ? Results.Ok(bug) : Results.NotFound();
        })
        .WithName("GetBugReport")
        .WithDescription("Get a single bug report by ID");

        // PATCH /api/bugs/{id} - Update bug status
        adminGroup.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateBugStatusRequest request,
            IBugReportService bugService) =>
        {
            var success = await bugService.UpdateBugStatusAsync(id, request);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("UpdateBugStatus")
        .WithDescription("Update bug report status and assignee");
    }
}
