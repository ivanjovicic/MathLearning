namespace MathLearning.Infrastructure.Services.Leaderboard;

/// <summary>
/// Gamification rules for leaderboard badges
/// </summary>
public static class BadgeRules
{
    /// <summary>
    /// Builds a list of badges based on rank, percentile, and scope
    /// </summary>
    public static List<string> BuildBadges(string scope, int percentile, int rank)
    {
        var badges = new List<string>();

        // General achievement badges
        if (rank == 1)
            badges.Add("🥇 #1 Champion");
        
        if (percentile <= 1)
            badges.Add("💎 Top 1%");
        else if (percentile <= 5)
            badges.Add("🔥 Top 5%");
        else if (percentile <= 10)
            badges.Add("⚡ Top 10%");

        // Context-specific badges
        if (scope == "faculty" && percentile <= 1)
            badges.Add("🎓 Top 1% at Faculty");
        
        if (scope == "school" && percentile <= 1)
            badges.Add("🏫 Top 1% at School");
        
        if (scope == "friends" && rank == 1)
            badges.Add("👥 Top Among Friends");

        return badges;
    }
}
