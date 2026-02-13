namespace MathLearning.Application.DTOs.Progress;

public record ProgressOverviewDto(
    int TotalAnswered,
    double Accuracy,
    int DailyStreak,
    int StreakFreezeCount = 0,
    DateOnly? LastStreakDay = null,
    DateOnly? LastActivityDay = null,
    StreakRollEventDto? StreakEvent = null
);

public record StreakRollEventDto(
    string Type,
    int MissedDays,
    int FreezesUsed,
    int StreakBefore,
    int StreakAfter
);
