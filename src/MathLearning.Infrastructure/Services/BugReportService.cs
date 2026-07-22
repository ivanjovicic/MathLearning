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

        string? screenshotStorageKey = null;
        if (!string.IsNullOrWhiteSpace(request.ScreenshotBase64))
        {
            screenshotStorageKey = CreateScreenshotStorageKey();
            var uploaded = await screenshotStorage.UploadScreenshotAsync(
                request.ScreenshotBase64,
                screenshotStorageKey);

            if (!uploaded)
            {
                screenshotStorageKey = null;
            }
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
            screenshotUrl: screenshotStorageKey);

        db.BugReports.Add(bugReport);

        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(screenshotStorageKey))
            {
                await screenshotStorage.DeleteScreenshotAsync(screenshotStorageKey);
            }

            throw;
        }

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
            .Select(b => new BugReportProjection(
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
            bugs.Select(Map).ToList(),
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
            .Select(b => new BugReportProjection(
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
            bugs.Select(Map).ToList(),
            totalCount,
            paging.Page,
            paging.PageSize);
    }

    public async Task<BugReportDto?> GetBugReportAsync(Guid id)
    {
        var bug = await db.BugReports.FirstOrDefaultAsync(b => b.Id == id);
        return bug is null ? null : Map(bug);
    }

    public Task<BugReportScreenshotInfo?> GetBugReportScreenshotInfoAsync(Guid id)
    {
        return db.BugReports
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BugReportScreenshotInfo(
                b.Id,
                b.UserId,
                b.ScreenshotUrl))
            .FirstOrDefaultAsync();
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
        bug.Id,
        bug.CreatedAt,
        bug.UserId,
        bug.UsernameSnapshot,
        bug.Screen,
        bug.Description,
        bug.StepsToReproduce,
        bug.Severity,
        bug.Platform,
        bug.Locale,
        bug.AppVersion,
        BuildScreenshotUrl(bug.Id, bug.ScreenshotUrl),
        bug.Status,
        bug.ResolvedAt,
        bug.Assignee);

    private static BugReportDto Map(BugReportProjection bug) => new(
        bug.Id,
        bug.CreatedAt,
        bug.UserId,
        bug.UsernameSnapshot,
        bug.Screen,
        bug.Description,
        bug.StepsToReproduce,
        bug.Severity,
        bug.Platform,
        bug.Locale,
        bug.AppVersion,
        BuildScreenshotUrl(bug.Id, bug.ScreenshotStorageKey),
        bug.Status,
        bug.ResolvedAt,
        bug.Assignee);

    private static string? BuildScreenshotUrl(Guid id, string? screenshotStorageKey) =>
        string.IsNullOrWhiteSpace(screenshotStorageKey)
            ? null
            : $"/api/bugs/{id}/screenshot";

    private static string CreateScreenshotStorageKey() => $"bugshot_{Guid.NewGuid():N}";

    private sealed record BugReportProjection(
        Guid Id,
        DateTime CreatedAt,
        string UserId,
        string UsernameSnapshot,
        string Screen,
        string Description,
        string? StepsToReproduce,
        string Severity,
        string Platform,
        string Locale,
        string AppVersion,
        string? ScreenshotStorageKey,
        string Status,
        DateTime? ResolvedAt,
        string? Assignee);
}
