using MathLearning.Application.DTOs.Bugs;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services;

public class BugReportService : IBugReportService
{
    private readonly ApiDbContext db;
    private readonly IScreenshotStorageService screenshotStorage;

    public BugReportService(ApiDbContext db, IScreenshotStorageService screenshotStorage)
    {
        this.db = db;
        this.screenshotStorage = screenshotStorage;
    }

    public async Task<BugReportDto> CreateBugReportAsync(string userId, BugReportRequest request)
    {
        if (!BugReportValidation.IsValidSeverity(request.Severity))
        {
            throw new ArgumentException(
                $"Invalid severity. Must be one of: {string.Join(", ", BugReportValidation.ValidSeverities)}");
        }

        var userProfile = await db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
        var username = userProfile?.Username ?? $"User{userId}";

        string? screenshotUrl = null;
        if (!string.IsNullOrWhiteSpace(request.ScreenshotBase64))
        {
            var fileName = $"{userId}_{Guid.NewGuid():N}.png";
            screenshotUrl = await screenshotStorage.UploadScreenshotAsync(request.ScreenshotBase64, fileName);
        }

        var bugReport = new BugReport(
            userId: userId,
            usernameSnapshot: username,
            screen: request.Screen,
            description: request.Description,
            stepsToReproduce: request.StepsToReproduce,
            severity: request.Severity.ToLowerInvariant(),
            platform: request.Platform,
            locale: request.Locale,
            appVersion: request.AppVersion,
            screenshotUrl: screenshotUrl);

        db.BugReports.Add(bugReport);
        await db.SaveChangesAsync();

        return Map(bugReport);
    }

    public async Task<BugReportsResponse> GetBugReportsAsync(
        int page = 1,
        int pageSize = 20,
        string? status = null,
        string? severity = null)
    {
        var requestedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var paging = PaginationBounds.Normalize(
            page,
            requestedPageSize,
            defaultPageSize: 20,
            maxPageSize: 100);
        var query = db.BugReports.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(b => b.Status == status);

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(b => b.Severity == severity);

        var totalCount = await query.CountAsync();
        var bugs = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(b => new BugReportDto(
                b.Id,
                b.CreatedAt,
                b.UserId,
                b.UsernameSnapshot,
                b.Screen,
                b.Description,
                b.StepsToReproduce,
                b.Severity,
                b.Platform,
                b.Locale,
                b.AppVersion,
                b.ScreenshotUrl,
                b.Status,
                b.ResolvedAt,
                b.Assignee))
            .ToListAsync();

        return new BugReportsResponse(
            bugs,
            totalCount,
            paging.Page,
            paging.PageSize);
    }

    public async Task<BugReportsResponse> GetMyBugReportsAsync(
        string userId,
        int page = 1,
        int pageSize = 50)
    {
        var requestedPageSize = pageSize is < 1 or > 100 ? 50 : pageSize;
        var paging = PaginationBounds.Normalize(
            page,
            requestedPageSize,
            defaultPageSize: 50,
            maxPageSize: 100);
        var query = db.BugReports.Where(b => b.UserId == userId);

        var totalCount = await query.CountAsync();
        var bugs = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(b => new BugReportDto(
                b.Id,
                b.CreatedAt,
                b.UserId,
                b.UsernameSnapshot,
                b.Screen,
                b.Description,
                b.StepsToReproduce,
                b.Severity,
                b.Platform,
                b.Locale,
                b.AppVersion,
                b.ScreenshotUrl,
                b.Status,
                b.ResolvedAt,
                b.Assignee))
            .ToListAsync();

        return new BugReportsResponse(
            bugs,
            totalCount,
            paging.Page,
            paging.PageSize);
    }

    public async Task<BugReportDto?> GetBugReportAsync(Guid id)
    {
        var bug = await db.BugReports.FirstOrDefaultAsync(b => b.Id == id);
        return bug is null ? null : Map(bug);
    }

    public async Task<bool> UpdateBugStatusAsync(Guid id, UpdateBugStatusRequest request)
    {
        if (!BugReportValidation.IsValidStatus(request.Status))
        {
            throw new ArgumentException(
                $"Invalid status. Must be one of: {string.Join(", ", BugReportValidation.ValidStatuses)}");
        }

        var bug = await db.BugReports.FirstOrDefaultAsync(b => b.Id == id);
        if (bug is null)
            return false;

        bug.UpdateStatus(request.Status.ToLowerInvariant(), request.Assignee);
        await db.SaveChangesAsync();
        return true;
    }

    private static BugReportDto Map(BugReport bug) => new(
        Id: bug.Id,
        CreatedAt: bug.CreatedAt,
        UserId: bug.UserId,
        UsernameSnapshot: bug.UsernameSnapshot,
        Screen: bug.Screen,
        Description: bug.Description,
        StepsToReproduce: bug.StepsToReproduce,
        Severity: bug.Severity,
        Platform: bug.Platform,
        Locale: bug.Locale,
        AppVersion: bug.AppVersion,
        ScreenshotUrl: bug.ScreenshotUrl,
        Status: bug.Status,
        ResolvedAt: bug.ResolvedAt,
        Assignee: bug.Assignee);
}
