namespace MathLearning.Application.DTOs.Quiz;

public record OfflineBatchSubmitIssue(
    int? QuestionId,
    string Code,
    string Message);
