using System.Text;
using System.Text.Json;

namespace MathLearning.Infrastructure.Services.Leaderboard;

/// <summary>
/// Encodes and decodes leaderboard cursors for pagination
/// </summary>
public static class CursorCodec
{
    private const int StudentCursorVersion = 2;
    private const int MaxCursorLength = 1024;
    private const int MaxDecodedJsonLength = 2048;

    private sealed record LegacyCursor(int Score, int Id);

    /// <summary>
    /// Encodes a cursor to a Base64 string
    /// </summary>
    public static string Encode(LeaderboardCursor cursor)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cursor)));

    public static string EncodeStudent(int score, string userId, string scope, string period)
        => Encode(new LeaderboardCursor(
            StudentCursorVersion,
            score,
            userId,
            Scope: scope,
            Period: period));

    public static string EncodeSchool(int score, int schoolId)
        => Encode(new LeaderboardCursor(1, score, SchoolId: schoolId));

    /// <summary>
    /// Decodes a Base64 cursor string
    /// </summary>
    public static LeaderboardCursor? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;

        if (cursor.Length > MaxCursorLength)
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            if (json.Length > MaxDecodedJsonLength)
                return null;

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (TryReadInt(root, "V", out var version))
            {
                return new LeaderboardCursor(
                    version,
                    ReadRequiredInt(root, "Score"),
                    ReadOptionalString(root, "UserId"),
                    TryReadInt(root, "SchoolId", out var schoolId) ? schoolId : null,
                    ReadOptionalString(root, "Scope"),
                    ReadOptionalString(root, "Period"));
            }

            var legacy = JsonSerializer.Deserialize<LegacyCursor>(json);
            return legacy is null
                ? null
                : new LeaderboardCursor(1, legacy.Score, SchoolId: legacy.Id);
        }
        catch
        {
            return null; // Invalid cursor
        }
    }

    public static LeaderboardCursor? DecodeStudentOrThrow(string? cursor, string scope, string period)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return null;

        if (cursor.Length > MaxCursorLength)
        {
            throw new LeaderboardCursorException(
                "cursor_too_large",
                "Leaderboard cursor is too large.");
        }

        var decoded = Decode(cursor);
        if (decoded is null)
        {
            throw new LeaderboardCursorException(
                "invalid_cursor",
                "Leaderboard cursor is invalid.");
        }

        if (decoded.V != StudentCursorVersion)
        {
            throw new LeaderboardCursorException(
                "unsupported_cursor_version",
                "Leaderboard cursor version is unsupported. Restart pagination from the first page.");
        }

        if (string.IsNullOrWhiteSpace(decoded.UserId) ||
            string.IsNullOrWhiteSpace(decoded.Scope) ||
            string.IsNullOrWhiteSpace(decoded.Period))
        {
            throw new LeaderboardCursorException(
                "invalid_cursor",
                "Leaderboard cursor is missing required fields.");
        }

        if (!string.Equals(decoded.Scope, scope, StringComparison.Ordinal) ||
            !string.Equals(decoded.Period, period, StringComparison.Ordinal))
        {
            throw new LeaderboardCursorException(
                "cursor_context_mismatch",
                "Leaderboard cursor does not match the requested scope/period.");
        }

        return decoded;
    }

    public static int? DecodeSchoolId(string? cursor)
    {
        var decoded = Decode(cursor);
        return decoded?.SchoolId;
    }

    private static bool TryReadInt(JsonElement root, string propertyName, out int value)
    {
        if (root.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static int ReadRequiredInt(JsonElement root, string propertyName)
        => TryReadInt(root, propertyName, out var value)
            ? value
            : throw new JsonException($"Missing required integer property '{propertyName}'.");

    private static string? ReadOptionalString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
