namespace MathLearning.Application.DTOs.Quiz;

public record QuizResponse(
    Guid QuizId,
    List<QuestionDto> Questions
);
