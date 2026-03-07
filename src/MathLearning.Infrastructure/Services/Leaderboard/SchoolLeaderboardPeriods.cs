namespace MathLearning.Infrastructure.Services.Leaderboard;

public readonly record struct SchoolLeaderboardPeriodInfo(string Period, DateTime PeriodStartUtc);

public static class SchoolLeaderboardPeriods
{
    public static readonly string[] All = ["day", "week", "month", "all_time"];

    public static SchoolLeaderboardPeriodInfo Normalize(string? period)
    {
        var normalized = (period ?? "week").Trim().ToLowerInvariant() switch
        {
            "day" or "daily" => "day",
            "month" or "monthly" => "month",
            "all" or "alltime" or "all_time" => "all_time",
            _ => "week"
        };

        var now = DateTime.UtcNow;
        var start = normalized switch
        {
            "day" => now.Date,
            "month" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            "all_time" => DateTime.UnixEpoch,
            _ => StartOfWeekUtc(now)
        };

        return new SchoolLeaderboardPeriodInfo(normalized, start);
    }

    public static DateTime StartOfWeekUtc(DateTime value)
    {
        var day = value.Date;
        var diff = ((int)day.DayOfWeek + 6) % 7;
        return day.AddDays(-diff);
    }
}
