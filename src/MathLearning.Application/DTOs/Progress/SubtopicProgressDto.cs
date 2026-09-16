namespace MathLearning.Application.DTOs.Progress;

public record SubtopicProgressDto(
    int SubtopicId,
    int TopicId,
    string Name,
    int PlayableQuestionCount,
    bool CanStartQuiz
);
