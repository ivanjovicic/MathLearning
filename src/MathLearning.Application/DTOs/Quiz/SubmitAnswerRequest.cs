namespace MathLearning.Application.DTOs.Quiz;

public record SubmitAnswerRequest(
    Guid QuizId,
    int QuestionId,
    string Answer,
    int TimeSpentSeconds
);
