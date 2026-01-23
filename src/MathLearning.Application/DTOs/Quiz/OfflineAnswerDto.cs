namespace MathLearning.Application.DTOs.Quiz;

public record OfflineAnswerDto(
    int QuestionId,
    string Answer,
    bool IsCorrectOffline,
    int TimeSpent,
    DateTime AnsweredAt
);
