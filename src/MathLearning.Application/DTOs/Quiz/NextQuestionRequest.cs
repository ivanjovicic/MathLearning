namespace MathLearning.Application.DTOs.Quiz;

public record NextQuestionRequest(
    Guid QuizId,
    int SubtopicId
);
