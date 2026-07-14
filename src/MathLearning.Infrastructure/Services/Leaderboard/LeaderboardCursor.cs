namespace MathLearning.Infrastructure.Services.Leaderboard;

/// <summary>
/// Cursor for keyset-based pagination in leaderboards.
/// </summary>
/// <param name="V">Cursor schema version.</param>
/// <param name="Score">The score value at cursor position.</param>
/// <param name="UserId">The canonical string user ID used for student leaderboard tie-breaks.</param>
/// <param name="SchoolId">The school ID used for school leaderboard tie-breaks.</param>
/// <param name="Scope">Normalized scope bound to the cursor.</param>
/// <param name="Period">Normalized period bound to the cursor.</param>
public sealed record LeaderboardCursor(
    int V,
    int Score,
    string? UserId = null,
    int? SchoolId = null,
    string? Scope = null,
    string? Period = null);
