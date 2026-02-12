namespace MathLearning.Application.DTOs.Quiz;

public record QuestionDto(
    int Id,
    string Type,
    string Text,
    List<OptionDto>? Options,
    int CorrectAnswerId,
    int Difficulty,
    string? HintLight,
    string? HintMedium,
    string? HintFull,
    string? Explanation,
    List<StepExplanationDto>? Steps
);
