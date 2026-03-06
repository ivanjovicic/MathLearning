namespace MathLearning.Core.DTOs;

/// <summary>
/// Represents a single entry in the leaderboard
/// </summary>
public record LeaderboardEntryDto(
    int Rank,
    string UserId,
    string DisplayName,
    int Level,
    int Xp,
    int WeeklyXp,
    int Streak
);