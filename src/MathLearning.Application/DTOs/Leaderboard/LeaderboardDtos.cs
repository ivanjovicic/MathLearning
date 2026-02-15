namespace MathLearning.Application.DTOs.Leaderboard;

/// <summary>
/// Represents a single entry in the leaderboard
/// </summary>
public record LeaderboardItemDto
{
    public int Rank { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public int Score { get; init; }
    public int StreakDays { get; init; }
    public int Level { get; init; }
}

/// <summary>
/// Context information for scoped leaderboards (school/faculty)
/// </summary>
public record LeaderboardContextDto
{
    public int? SchoolId { get; init; }
    public string? SchoolName { get; init; }
    public int? FacultyId { get; init; }
    public string? FacultyName { get; init; }
}

/// <summary>
/// Current user's position and achievements in the leaderboard
/// </summary>
public record LeaderboardMeDto
{
    public int Rank { get; init; }
    public int Score { get; init; }
    public int Percentile { get; init; } // 1-100, where 1 = top 1%
    public List<string> Badges { get; init; } = new();
}

/// <summary>
/// Complete leaderboard response with top users and current user position
/// </summary>
public record LeaderboardResponseDto
{
    public string Scope { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public LeaderboardContextDto? Context { get; init; }
    public List<LeaderboardItemDto> Items { get; init; } = new();
    public LeaderboardMeDto? Me { get; init; }
    public string? NextCursor { get; init; }
}

/// <summary>
/// School leaderboard entry (for school vs school rankings)
/// </summary>
public record SchoolLeaderboardItemDto
{
    public int Rank { get; init; }
    public int SchoolId { get; init; }
    public string SchoolName { get; init; } = string.Empty;
    public int Score { get; init; }
    public int Members { get; init; }
}

/// <summary>
/// Response for school-level aggregate leaderboards
/// </summary>
public record SchoolLeaderboardResponseDto
{
    public string Period { get; init; } = string.Empty;
    public List<SchoolLeaderboardItemDto> Items { get; init; } = new();
    public SchoolLeaderboardItemDto? MySchool { get; init; }
    public string? NextCursor { get; init; }
}
