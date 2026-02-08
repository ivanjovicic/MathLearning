namespace MathLearning.Application.DTOs.Quiz;

public record NextQuestionResponse(
    int Id,
    string Type,
    string Text,
    List<OptionDto>? Options,
    int Difficulty,
    string? HintLight,
    string? HintMedium,
    string? HintFull,
    string? Explanation
);
