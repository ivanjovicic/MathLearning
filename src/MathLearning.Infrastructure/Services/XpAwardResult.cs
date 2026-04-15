namespace MathLearning.Infrastructure.Services;

public sealed record XpAwardResult(
    int AwardedXp,
    int TotalXpAfterAward,
    string Reason,
    int RetryCount);
