namespace MathLearning.Application.DTOs.Quiz;

public record StepExplanationDto(
    string Text,
    string? Hint,
    bool Highlight
);
