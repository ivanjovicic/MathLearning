using MathLearning.Application.DTOs.Bugs;
using MathLearning.Application.Services;

namespace MathLearning.Api.Endpoints;

public static class BugEndpoints
{
    public static void MapBugEndpoints(this IEndpointRouteBuilder app)
    {
        var userGroup = app.MapGroup("/api/bugs")
            .RequireAuthorization()
            .WithTags("BugReports");

        var adminGroup = app.MapGroup("/api/bugs")
            .RequireAuthorization(DesignTokenSecurity.AdminPolicy)
            .WithTags("BugReports");

        // POST /api/bugs/report - Submit a report as the authenticated user.
        userGroup.MapPost("/report", async (
            BugReportRequest request,
            IBugReportService bugService,
            HttpContext ctx) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            var result = await bugService.CreateBugReportAsync(userId, request);
            return Results.Created($"/api/bugs/{result.Id}", result);
        })
        .WithName("ReportBug")
        .WithDescription("Report a bug from the authenticated frontend user");

        // GET /api/bugs/mine - Get the authenticated user's reports.
        userGroup.MapGet("/mine", async (
            IBugReportService bugService,
            HttpContext ctx,
            int page = 1,
            int pageSize = 50) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var result = await bugService.GetMyBugReportsAsync(userId, page, pageSize);
            return Results.Ok(result);
        })
        .WithName("GetMyBugReports")
        .WithDescription("Get my submitted bug reports");

        // GET /api/bugs - List all reports (admin only).
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

        // GET /api/bugs/{id} - Get a single report (admin only).
        adminGroup.MapGet("/{id:guid}", async (
            Guid id,
            IBugReportService bugService) =>
        {
            var bug = await bugService.GetBugReportAsync(id);
            return bug != null ? Results.Ok(bug) : Results.NotFound();
        })
        .WithName("GetBugReport")
        .WithDescription("Get a single bug report by ID");

        // PATCH /api/bugs/{id} - Update report status (admin only).
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
