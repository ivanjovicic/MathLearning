using MathLearning.Application.DTOs.Bugs;
using MathLearning.Application.Helpers;
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

        userGroup.MapGet("/mine", async (
            IBugReportService bugService,
            HttpContext ctx,
            int page = 1,
            int pageSize = 50) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            var requestedPageSize = pageSize is < 1 or > 100 ? 50 : pageSize;
            var paging = PaginationBounds.Normalize(
                page,
                requestedPageSize,
                defaultPageSize: 50,
                maxPageSize: 100);
            var result = await bugService.GetMyBugReportsAsync(
                userId,
                paging.Page,
                paging.PageSize);
            return Results.Ok(result);
        })
        .WithName("GetMyBugReports")
        .WithDescription("Get my submitted bug reports");

        userGroup.MapGet("/{id:guid}/screenshot", async (
            Guid id,
            IBugReportService bugService,
            IScreenshotStorageService screenshotStorage,
            HttpContext ctx) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            var screenshotInfo = await bugService.GetBugReportScreenshotInfoAsync(id);
            if (screenshotInfo is null)
            {
                return Results.NotFound(new { error = "Bug report not found" });
            }

            var isAdmin = ctx.User.IsInRole(DesignTokenSecurity.AdminRole);
            if (!isAdmin && !string.Equals(screenshotInfo.UserId, userId, StringComparison.Ordinal))
            {
                return Results.Forbid();
            }

            if (string.IsNullOrWhiteSpace(screenshotInfo.ScreenshotStorageKey))
            {
                return Results.NotFound(new { error = "Screenshot not available" });
            }

            var screenshot = await screenshotStorage.GetScreenshotAsync(screenshotInfo.ScreenshotStorageKey);
            if (screenshot is null)
            {
                return Results.NotFound(new { error = "Screenshot not available" });
            }

            return Results.File(screenshot.Bytes, screenshot.ContentType);
        })
        .WithName("GetBugScreenshot")
        .WithDescription("Get a private bug screenshot when you are the reporter or an admin");

        adminGroup.MapGet("/", async (
            IBugReportService bugService,
            int page = 1,
            int pageSize = 20,
            string? status = null,
            string? severity = null) =>
        {
            var requestedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            var paging = PaginationBounds.Normalize(
                page,
                requestedPageSize,
                defaultPageSize: 20,
                maxPageSize: 100);
            var result = await bugService.GetBugReportsAsync(
                paging.Page,
                paging.PageSize,
                status,
                severity);
            return Results.Ok(result);
        })
        .WithName("GetBugReports")
        .WithDescription("Get paginated list of bug reports");

        adminGroup.MapGet("/{id:guid}", async (
            Guid id,
            IBugReportService bugService) =>
        {
            var bug = await bugService.GetBugReportAsync(id);
            return bug != null ? Results.Ok(bug) : Results.NotFound();
        })
        .WithName("GetBugReport")
        .WithDescription("Get a single bug report by ID");

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
