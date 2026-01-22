namespace MathLearning.Application.DTOs.Quiz;

public record StartQuizRequest(
    int SubtopicId,
    int QuestionCount
);
