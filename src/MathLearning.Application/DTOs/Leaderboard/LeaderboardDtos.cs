using MathLearning.Application.DTOs.Cosmetics;

namespace MathLearning.Application.DTOs.Leaderboard;

public record LeaderboardItemDto
{
    public int Rank { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public AvatarAppearanceDto? Appearance { get; init; }
    public int Score { get; init; }
    public int StreakDays { get; init; }
    public int Level { get; init; }
}

public record LeaderboardContextDto
{
    public int? SchoolId { get; init; }
    public string? SchoolName { get; init; }
    public int? FacultyId { get; init; }
    public string? FacultyName { get; init; }
}

public record LeaderboardMeDto
{
    public int Rank { get; init; }
    public int Score { get; init; }
    public int Percentile { get; init; }
    public List<string> Badges { get; init; } = new();
}

public record LeaderboardResponseDto
{
    public string Scope { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public LeaderboardContextDto? Context { get; init; }
    public List<LeaderboardItemDto> Items { get; init; } = new();
    public LeaderboardMeDto? Me { get; init; }
    public string? NextCursor { get; init; }
}

public record SchoolLeaderboardItemDto
{
    public int Rank { get; init; }
    public int SchoolId { get; init; }
    public string SchoolName { get; init; } = string.Empty;
    public int Score { get; init; }
    public int Members { get; init; }
    public decimal? RankingScore { get; init; }
    public int? ActiveStudents { get; init; }
    public int? EligibleStudents { get; init; }
    public decimal? ParticipationRate { get; init; }
    public decimal? AverageXpPerActiveStudent { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public record SchoolLeaderboardResponseDto
{
    public string Period { get; init; } = string.Empty;
    public List<SchoolLeaderboardItemDto> Items { get; init; } = new();
    public SchoolLeaderboardItemDto? MySchool { get; init; }
    public string? NextCursor { get; init; }
    public string RankingMetric { get; init; } = "composite_score";
    public DateTime? PeriodStartUtc { get; init; }
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
    public bool IsStale { get; init; }
}

public record SchoolLeaderboardDetailDto
{
    public string Period { get; init; } = string.Empty;
    public DateTime? PeriodStartUtc { get; init; }
    public SchoolLeaderboardItemDto School { get; init; } = new();
    public List<SchoolLeaderboardItemDto> NearbySchools { get; init; } = new();
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
}

public record SchoolLeaderboardHistoryPointDto
{
    public DateTime SnapshotTimeUtc { get; init; }
    public int Rank { get; init; }
    public int Score { get; init; }
    public int ActiveStudents { get; init; }
    public decimal ParticipationRate { get; init; }
    public decimal CompositeScore { get; init; }
}

public record SchoolLeaderboardHistoryResponseDto
{
    public int SchoolId { get; init; }
    public string Period { get; init; } = string.Empty;
    public DateTime? PeriodStartUtc { get; init; }
    public List<SchoolLeaderboardHistoryPointDto> Points { get; init; } = new();
}
