namespace MathLearning.Infrastructure.Services.Leaderboard;

/// <summary>
/// Cursor for keyset-based pagination in leaderboards
/// </summary>
/// <param name="Score">The score value at cursor position</param>
/// <param name="Id">The unique ID (UserId or SchoolId) at cursor position</param>
public sealed record LeaderboardCursor(int Score, int Id);
