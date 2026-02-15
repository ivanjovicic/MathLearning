namespace MathLearning.Domain.Entities;

public class BugReport
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public string UserId { get; private set; } = string.Empty;
    public string UsernameSnapshot { get; private set; } = "";
    public string Screen { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string? StepsToReproduce { get; private set; }
    public string Severity { get; private set; } = "medium";
    public string Platform { get; private set; } = "";
    public string Locale { get; private set; } = "";
    public string AppVersion { get; private set; } = "";
    public string? ScreenshotUrl { get; private set; }
    public string Status { get; private set; } = "open";
    public DateTime? ResolvedAt { get; private set; }
    public string? Assignee { get; private set; }

    private BugReport() { }

    public BugReport(
        string userId,
        string usernameSnapshot,
        string screen,
        string description,
        string? stepsToReproduce,
        string severity,
        string platform,
        string locale,
        string appVersion,
        string? screenshotUrl)
    {
        UserId = userId;
        UsernameSnapshot = usernameSnapshot;
        Screen = screen;
        Description = description;
        StepsToReproduce = stepsToReproduce;
        Severity = severity;
        Platform = platform;
        Locale = locale;
        AppVersion = appVersion;
        ScreenshotUrl = screenshotUrl;
    }

    public void UpdateStatus(string status, string? assignee = null)
    {
        Status = status;
        Assignee = assignee;

        if (status == "fixed" || status == "closed")
        {
            ResolvedAt = DateTime.UtcNow;
        }
        else
        {
            ResolvedAt = null;
        }
    }
}
