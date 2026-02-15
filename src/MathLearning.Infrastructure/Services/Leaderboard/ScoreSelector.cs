using MathLearning.Domain.Entities;

namespace MathLearning.Infrastructure.Services.Leaderboard;

/// <summary>
/// Centralized score selection based on time period
/// </summary>
public static class ScoreSelector
{
    /// <summary>
    /// Gets the score for a user based on the specified period
    /// </summary>
    public static int ScoreOf(UserProfile user, string period) => period.ToLower() switch
    {
        "day" => user.DailyXp,
        "week" => user.WeeklyXp,
        "month" => user.MonthlyXp,
        _ => user.Xp // all_time
    };
}
