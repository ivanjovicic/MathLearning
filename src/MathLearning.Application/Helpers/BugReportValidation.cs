namespace MathLearning.Application.Helpers;

public static class BugReportValidation
{
    public static readonly string[] ValidSeverities = new[] { "low", "medium", "high", "critical" };
    public static readonly string[] ValidStatuses = new[] { "open", "in_progress", "fixed", "closed" };

    public static bool IsValidSeverity(string severity)
    {
        return ValidSeverities.Contains(severity?.ToLowerInvariant());
    }

    public static bool IsValidStatus(string status)
    {
        return ValidStatuses.Contains(status?.ToLowerInvariant());
    }
}
