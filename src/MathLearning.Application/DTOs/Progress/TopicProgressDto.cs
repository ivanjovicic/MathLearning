namespace MathLearning.Application.DTOs.Progress;

public record TopicProgressDto(
    int TopicId,
    string Name,
    double Accuracy,
    bool Unlocked,
    int PlayableQuestionCount,
    bool CanStartQuiz
);
