namespace MathLearning.Domain.Entities;

public class UserProfile
{
    // Same as AspNetUsers.Id (Identity key). Keep this stable; do not hash/parse.
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; } // Optional display name
    public string? AvatarUrl { get; set; }

    // 🎓 Optional education metadata (nullable by requirement)
    public string? SchoolName { get; set; }
    public string? FacultyName { get; set; }
    public int? SchoolId { get; set; }
    public int? FacultyId { get; set; }

    // ❄️ Streak freeze power-up
    public int StreakFreezeCount { get; set; } = 0;
    public DateOnly? LastStreakDay { get; set; }
    public DateOnly? LastActivityDay { get; set; }
    
    // 💰 Coin System
    public int Coins { get; set; } = 100; // Start with 100 coins
    public int TotalCoinsEarned { get; set; } = 0;
    public int TotalCoinsSpent { get; set; } = 0;
    
    // 📊 Stats
    public int Level { get; set; } = 1;
    public int Xp { get; set; } = 0;
    public int Streak { get; set; } = 0;
    
    // 📈 Time-based XP tracking for leaderboards
    public int DailyXp { get; set; } = 0;
    public int WeeklyXp { get; set; } = 0;
    public int MonthlyXp { get; set; } = 0;
    public DateTime? LastXpResetDate { get; set; }
    
    // 🏆 Leaderboard opt-in (privacy setting)
    public bool LeaderboardOptIn { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
