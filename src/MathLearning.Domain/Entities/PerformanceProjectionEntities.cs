namespace MathLearning.Domain.Entities;

public class LeaderboardSnapshot
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public int Rank { get; set; }
    public int Score { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Streak { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class UserQuizSummary
{
    public string UserId { get; set; } = string.Empty;
    public int TotalCorrect { get; set; }
    public int TotalAttempts { get; set; }
    public int WeeklyCorrect { get; set; }
    public int WeeklyXp { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class UserRewardState
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string RewardKey { get; set; } = string.Empty;
    public bool Eligible { get; set; }
    public bool Claimed { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
