namespace MathLearning.Domain.Events;

public sealed record QuizCompleted(
    string UserId,
    int TopicId,
    int Correct,
    int Total,
    int XpGained
) : DomainEventBase;
