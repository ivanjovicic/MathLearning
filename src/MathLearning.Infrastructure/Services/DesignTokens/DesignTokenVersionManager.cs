using System.Globalization;
using System.Text.RegularExpressions;
using MathLearning.Application.Services;

namespace MathLearning.Infrastructure.Services.DesignTokens;

public sealed class DesignTokenVersionManager : IDesignTokenVersionManager
{
    public string EnsureNextVersion(string? requestedVersion, string? currentVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            var initial = string.IsNullOrWhiteSpace(requestedVersion) ? "1.0.0" : requestedVersion.Trim();
            _ = SemanticVersion.Parse(initial);
            return initial;
        }

        var current = SemanticVersion.Parse(currentVersion);
        var candidate = string.IsNullOrWhiteSpace(requestedVersion)
            ? current.BumpPatch().ToString()
            : requestedVersion.Trim();

        var next = SemanticVersion.Parse(candidate);
        if (next.CompareTo(current) <= 0)
        {
            throw new InvalidOperationException($"Version '{candidate}' must be greater than '{currentVersion}'.");
        }

        return candidate;
    }

    public string CreateRollbackVersion(string? requestedVersion, string currentVersion) =>
        EnsureNextVersion(requestedVersion, currentVersion);

    private readonly record struct SemanticVersion(int Major, int Minor, int Patch)
    {
        private static readonly Regex SemverRegex = new("^(?<major>\\d+)\\.(?<minor>\\d+)\\.(?<patch>\\d+)$", RegexOptions.Compiled);

        public static SemanticVersion Parse(string value)
        {
            var match = SemverRegex.Match(value);
            if (!match.Success)
            {
                throw new InvalidOperationException($"Version '{value}' is not a valid semantic version.");
            }

            return new SemanticVersion(
                int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture));
        }

        public SemanticVersion BumpPatch() => this with { Patch = Patch + 1 };

        public int CompareTo(SemanticVersion other)
        {
            var major = Major.CompareTo(other.Major);
            if (major != 0) return major;
            var minor = Minor.CompareTo(other.Minor);
            if (minor != 0) return minor;
            return Patch.CompareTo(other.Patch);
        }

        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }
}
