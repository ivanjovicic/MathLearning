namespace MathLearning.Application.DTOs.Quiz;

public record OfflineBatchSubmitResponse(
    int ImportedCount,
    int NewXp,
    int NewLevel,
    int Streak
);
