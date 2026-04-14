namespace MathLearning.Application.Services;

public sealed record BktParameters(
    decimal PL0,
    decimal PT,
    decimal PG,
    decimal PS);

public interface IBktService
{
    BktParameters GetParamsForTopic(int topicId);

    decimal UpdateMastery(
        decimal prior,
        bool isCorrect,
        BktParameters parameters);
}
