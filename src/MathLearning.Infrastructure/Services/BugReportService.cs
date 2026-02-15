using MathLearning.Application.DTOs.Bugs;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services;

public class BugReportService : IBugReportService
{
    private readonly ApiDbContext _db;
    private readonly IScreenshotStorageService _screenshotStorage;

    public BugReportService(ApiDbContext db, IScreenshotStorageService screenshotStorage)
    {
        _db = db;
        _screenshotStorage = screenshotStorage;
    }

    public async Task<BugReportDto> CreateBugReportAsync(string userId, BugReportRequest request)
    {
        // Validate severity
        if (!BugReportValidation.IsValidSeverity(request.Severity))
        {
            throw new ArgumentException($"Invalid severity. Must be one of: {string.Join(", ", BugReportValidation.ValidSeverities)}");
        }

        // Get username snapshot
        var userProfile = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        var username = userProfile?.Username ?? $"User{userId}";

        // Upload screenshot if provided
        string? screenshotUrl = null;
        if (!string.IsNullOrWhiteSpace(request.ScreenshotBase64))
        {
            var fileName = $"{userId}_{Guid.NewGuid():N}.png";
            screenshotUrl = await _screenshotStorage.UploadScreenshotAsync(request.ScreenshotBase64, fileName);
        }

        // Create bug report
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
            screenshotUrl: screenshotUrl
        );

        _db.BugReports.Add(bugReport);
        await _db.SaveChangesAsync();

        return new BugReportDto(
            Id: bugReport.Id,
            CreatedAt: bugReport.CreatedAt,
            UserId: bugReport.UserId,
            UsernameSnapshot: bugReport.UsernameSnapshot,
            Screen: bugReport.Screen,
            Description: bugReport.Description,
            StepsToReproduce: bugReport.StepsToReproduce,
            Severity: bugReport.Severity,
            Platform: bugReport.Platform,
            Locale: bugReport.Locale,
            AppVersion: bugReport.AppVersion,
            ScreenshotUrl: bugReport.ScreenshotUrl,
            Status: bugReport.Status,
            ResolvedAt: bugReport.ResolvedAt,
            Assignee: bugReport.Assignee
        );
    }

    public async Task<BugReportsResponse> GetBugReportsAsync(int page = 1, int pageSize = 20, string? status = null, string? severity = null)
    {
        var query = _db.BugReports.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(b => b.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            query = query.Where(b => b.Severity == severity);
        }

        var totalCount = await query.CountAsync();

        var bugs = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
                b.Assignee
            ))
            .ToListAsync();

        return new BugReportsResponse(bugs, totalCount, page, pageSize);
    }

    public async Task<BugReportsResponse> GetMyBugReportsAsync(string userId, int page = 1, int pageSize = 50)
    {
        var query = _db.BugReports
            .Where(b => b.UserId == userId);

        var totalCount = await query.CountAsync();

        var bugs = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
                b.Assignee
            ))
            .ToListAsync();

        return new BugReportsResponse(bugs, totalCount, page, pageSize);
    }

    public async Task<BugReportDto?> GetBugReportAsync(Guid id)
    {
        var bug = await _db.BugReports
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bug == null) return null;

        return new BugReportDto(
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
            Assignee: bug.Assignee
        );
    }

    public async Task<bool> UpdateBugStatusAsync(Guid id, UpdateBugStatusRequest request)
    {
        // Validate status
        if (!BugReportValidation.IsValidStatus(request.Status))
        {
            throw new ArgumentException($"Invalid status. Must be one of: {string.Join(", ", BugReportValidation.ValidStatuses)}");
        }

        var bug = await _db.BugReports.FirstOrDefaultAsync(b => b.Id == id);
        if (bug == null) return false;

        bug.UpdateStatus(request.Status.ToLowerInvariant(), request.Assignee);
        await _db.SaveChangesAsync();

        return true;
    }
}
