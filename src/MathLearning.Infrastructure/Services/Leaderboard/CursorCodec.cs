using System.Text;
using System.Text.Json;

namespace MathLearning.Infrastructure.Services.Leaderboard;

/// <summary>
/// Encodes and decodes leaderboard cursors for pagination
/// </summary>
public static class CursorCodec
{
    /// <summary>
    /// Encodes a cursor to a Base64 string
    /// </summary>
    public static string Encode(LeaderboardCursor cursor)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cursor)));

    /// <summary>
    /// Decodes a Base64 cursor string
    /// </summary>
    public static LeaderboardCursor? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return JsonSerializer.Deserialize<LeaderboardCursor>(json);
        }
        catch
        {
            return null; // Invalid cursor
        }
    }
}
