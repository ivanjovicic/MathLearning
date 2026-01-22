namespace MathLearning.Application.DTOs.Progress;

public record TopicProgressDto(
    int TopicId,
    string TopicName,
    double Accuracy
);
