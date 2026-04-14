using MathLearning.Domain.Entities;

namespace MathLearning.Api.Services;

internal static class MasteryScoringHelper
{
    public static double CalculateMasteryScore(
        int attempts,
        int correctAttempts,
        double averageConfidence,
        DateTime? lastPracticedAt,
        double previousMastery,
        DateTime nowUtc,
        double decayFactorDays = 14d)
    {
        if (attempts <= 0)
            return 0d;

        var safeAttempts = Math.Max(1, attempts);
        var accuracyScore = Math.Clamp((double)correctAttempts / safeAttempts, 0d, 1d);

        var daysSincePractice = lastPracticedAt.HasValue
            ? Math.Max(0d, (nowUtc - lastPracticedAt.Value).TotalDays)
            : decayFactorDays * 2d;

        var recencyDecay = Math.Exp(-daysSincePractice / Math.Max(1d, decayFactorDays));
        var confidenceScore = Math.Clamp(averageConfidence, 0d, 1d);
        var confidenceWeight = Math.Min(1d, safeAttempts / 10d);

        // Weighted blend:
        // - accuracy drives knowledge correctness
        // - recency prevents stale mastery from looking inflated
        // - confidence helps distinguish lucky guesses from stable skill
        var rawScore = (0.6 * accuracyScore) + (0.25 * recencyDecay) + (0.15 * confidenceScore);
        var normalized = Math.Clamp(rawScore * 100d, 0d, 100d);

        // Smooth with confidenceWeight so low-sample topics don't oscillate.
        var smoothed = (previousMastery * (1d - confidenceWeight)) + (normalized * confidenceWeight);
        return Math.Round(smoothed, 2);
    }
}

internal static class DifficultyPolicyHelper
{
    private static readonly IReadOnlyDictionary<string, double> PromotionResponseThresholds =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            [AdaptiveDifficultyLevels.Easy] = 18d,
            [AdaptiveDifficultyLevels.Medium] = 25d,
            [AdaptiveDifficultyLevels.Hard] = 35d
        };

    public sealed record DifficultyDecision(string Difficulty, bool Changed);

    public static DifficultyDecision Decide(
        string currentDifficulty,
        double rollingAccuracy,
        double averageResponseSeconds,
        DateTime? lastDifficultyChangeAt,
        DateTime nowUtc,
        int rollingSampleSize)
    {
        var current = AdaptiveDifficultyLevels.Normalize(currentDifficulty);
        var samples = Math.Max(rollingSampleSize, 0);

        // Require enough history and respect cooldown to avoid oscillation.
        if (samples < 8 || IsInCooldown(lastDifficultyChangeAt, nowUtc))
            return new DifficultyDecision(current, false);

        var next = current;
        var responseThreshold = PromotionResponseThresholds[current];

        if (rollingAccuracy > 0.80 && averageResponseSeconds > 0 && averageResponseSeconds < responseThreshold)
        {
            next = Promote(current);
        }
        else if (rollingAccuracy < 0.50)
        {
            next = Demote(current);
        }

        return new DifficultyDecision(next, !string.Equals(next, current, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInCooldown(DateTime? lastDifficultyChangeAt, DateTime nowUtc)
    {
        if (!lastDifficultyChangeAt.HasValue)
            return false;

        return (nowUtc - lastDifficultyChangeAt.Value).TotalDays < 3d;
    }

    private static string Promote(string difficulty) =>
        difficulty switch
        {
            AdaptiveDifficultyLevels.Easy => AdaptiveDifficultyLevels.Medium,
            AdaptiveDifficultyLevels.Medium => AdaptiveDifficultyLevels.Hard,
            _ => AdaptiveDifficultyLevels.Hard
        };

    private static string Demote(string difficulty) =>
        difficulty switch
        {
            AdaptiveDifficultyLevels.Hard => AdaptiveDifficultyLevels.Medium,
            AdaptiveDifficultyLevels.Medium => AdaptiveDifficultyLevels.Easy,
            _ => AdaptiveDifficultyLevels.Easy
        };
}

internal static class Sm2SchedulerHelper
{
    public const int MaxIntervalDays = 120;

    public sealed record ScheduleResult(
        double EasinessFactor,
        int IntervalDays,
        int RepetitionCount,
        DateTime DueAt);

    public static ScheduleResult Compute(
        bool isCorrect,
        double confidence,
        double currentEasinessFactor,
        int currentIntervalDays,
        int currentRepetitionCount,
        DateTime nowUtc)
    {
        var ef = Math.Clamp(currentEasinessFactor, 1.3d, 2.8d);
        var repetition = Math.Max(0, currentRepetitionCount);
        var interval = Math.Max(1, currentIntervalDays);

        if (!isCorrect)
        {
            repetition = 0;
            interval = 1;
            ef = Math.Max(1.3d, ef - 0.2d);
            return new ScheduleResult(ef, interval, repetition, nowUtc.AddDays(interval));
        }

        repetition++;

        interval = repetition switch
        {
            1 => 1,
            2 => 6,
            _ => Math.Min(MaxIntervalDays, Math.Max(1, (int)Math.Round(interval * ef)))
        };

        var quality = ConfidenceToQuality(confidence);
        ef = Math.Clamp(
            ef + (0.1d - (5 - quality) * (0.08d + (5 - quality) * 0.02d)),
            1.3d,
            2.8d);

        return new ScheduleResult(ef, interval, repetition, nowUtc.AddDays(interval));
    }

    private static int ConfidenceToQuality(double confidence)
    {
        var normalized = Math.Clamp(confidence, 0d, 1d);
        return normalized switch
        {
            >= 0.9 => 5,
            >= 0.7 => 4,
            >= 0.5 => 3,
            >= 0.3 => 2,
            _ => 1
        };
    }
}

internal static class AdaptiveDifficultyMapper
{
    public static string FromQuestionDifficulty(int difficulty) =>
        difficulty switch
        {
            <= 2 => AdaptiveDifficultyLevels.Easy,
            3 => AdaptiveDifficultyLevels.Medium,
            _ => AdaptiveDifficultyLevels.Hard
        };

    public static int DistanceFrom(string preferredDifficulty, int questionDifficulty)
    {
        var target = preferredDifficulty switch
        {
            AdaptiveDifficultyLevels.Easy => 2,
            AdaptiveDifficultyLevels.Medium => 3,
            AdaptiveDifficultyLevels.Hard => 4,
            _ => 3
        };

        return Math.Abs(target - questionDifficulty);
    }
}
