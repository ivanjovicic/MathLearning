namespace MathLearning.Application.DTOs.Progress;

public record ProgressOverviewDto(
    int TotalAnswered,
    double Accuracy,
    int DailyStreak
);
