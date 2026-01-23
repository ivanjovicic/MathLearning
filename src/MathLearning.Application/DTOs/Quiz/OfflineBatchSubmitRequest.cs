namespace MathLearning.Application.DTOs.Quiz;

public record OfflineBatchSubmitRequest(
    string SessionId,
    List<OfflineAnswerDto> Answers
);
