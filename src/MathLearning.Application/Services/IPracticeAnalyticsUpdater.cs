namespace MathLearning.Application.Services;

public sealed record PracticeAttemptAnalyticsInput(
    string UserId,
    Guid SessionId,
    int QuestionId,
    int TopicId,
    int SubtopicId,
    bool IsCorrect,
    int TimeSpentMs,
    DateTime AttemptedAtUtc);

public interface IPracticeAnalyticsUpdater
{
    Task UpdateAggregatesAsync(PracticeAttemptAnalyticsInput attempt, CancellationToken ct = default);

    Task UpdateDailyActivityAsync(
        string userId,
        DateOnly day,
        bool completed,
        CancellationToken ct = default);

    Task TriggerWeaknessRecomputeAsync(string userId, CancellationToken ct = default);
}
