namespace MathLearning.Application.Services;

public interface IQuizAttemptIngestService
{
    Task IngestAttemptsAsync(
        string userId,
        IReadOnlyList<QuizAttemptIngestItem> attempts,
        CancellationToken ct = default);
}

public sealed record QuizAttemptIngestItem(
    string AttemptKey,
    Guid QuizId,
    int QuestionId,
    int SubtopicId,
    bool Correct,
    int TimeSpentMs,
    DateTime CreatedAtUtc);
