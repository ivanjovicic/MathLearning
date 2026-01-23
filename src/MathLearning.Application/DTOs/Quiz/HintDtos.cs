namespace MathLearning.Application.DTOs.Quiz;

public record HintRequest(
    int QuestionId,
    string HintType // "formula", "clue", "solution"
);

public record HintResponse(
    string HintType,
    string? HintContent,
    int HintDifficulty,
    bool Success,
    string? Message = null
);

public record HintStatsDto(
    int TotalHintsUsed,
    int FormulaHintsUsed,
    int ClueHintsUsed,
    int SolutionHintsUsed,
    double AverageHintsPerQuestion
);
