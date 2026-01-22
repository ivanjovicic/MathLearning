namespace MathLearning.Application.DTOs.Quiz;

public record QuestionDto(
    int Id,
    string Type,
    string Text,
    List<OptionDto>? Options
);
