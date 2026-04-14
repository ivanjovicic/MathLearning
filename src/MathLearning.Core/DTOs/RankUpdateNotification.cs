namespace MathLearning.Core.DTOs;

public record RankUpdateNotification(
    int Rank,
    int Xp,
    int WeeklyXp,
    int Streak
);