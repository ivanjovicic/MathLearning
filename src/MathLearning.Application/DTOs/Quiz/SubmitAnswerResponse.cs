namespace MathLearning.Application.DTOs.Quiz;

public record SubmitAnswerResponse(
    bool IsCorrect,
    string? Explanation,
    List<StepExplanationDto>? Steps = null,
    bool IsFirstTimeCorrect = false,
    int AwardedXp = 0,
    int TotalXp = 0
);
