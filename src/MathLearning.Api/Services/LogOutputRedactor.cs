using System.Text.RegularExpressions;

namespace MathLearning.Api.Services;

public static partial class LogOutputRedactor
{
    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"Bearer\s+\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(@"(?i)(password|pwd|secret|api[_-]?key|token|connectionstring)\s*[=:]\s*\S+")]
    private static partial Regex SecretAssignmentPattern();

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var redacted = EmailPattern().Replace(value, "[redacted-email]");
        redacted = BearerTokenPattern().Replace(redacted, "[redacted-token]");
        redacted = SecretAssignmentPattern().Replace(redacted, "$1=[redacted]");
        return redacted;
    }

    public static IReadOnlyList<string> RedactLines(IEnumerable<string> lines) =>
        lines.Select(Redact).ToList();
}
