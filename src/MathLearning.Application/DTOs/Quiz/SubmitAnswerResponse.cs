namespace MathLearning.Application.DTOs.Quiz;

public record SubmitAnswerResponse(
    bool IsCorrect,
    string? Explanation
);
