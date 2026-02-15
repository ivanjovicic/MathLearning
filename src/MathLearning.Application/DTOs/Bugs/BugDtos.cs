namespace MathLearning.Application.DTOs.Bugs;

public record BugReportRequest(
    string Screen,
    string Description,
    string? StepsToReproduce,
    string Severity,
    string Platform,
    string Locale,
    string AppVersion,
    string? ScreenshotBase64
);

public record BugReportDto(
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
    string? ScreenshotUrl,
    string Status,
    DateTime? ResolvedAt,
    string? Assignee
);

public record UpdateBugStatusRequest(
    string Status,
    string? Assignee
);

public record BugReportsResponse(
    List<BugReportDto> Bugs,
    int TotalCount,
    int Page,
    int PageSize
);
