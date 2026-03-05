namespace MathLearning.Api.Services;

public static class WeaknessScoring
{
    public static decimal CalculateAccuracy(int correctAnswers, int totalQuestions)
    {
        if (totalQuestions <= 0)
            return 0m;

        return decimal.Round((decimal)correctAnswers / totalQuestions, 4, MidpointRounding.AwayFromZero);
    }

    public static double CalculateAttemptFactor(int totalQuestions)
    {
        var raw = Math.Log10(totalQuestions + 1d);
        return Math.Clamp(raw, 0d, 1.5d);
    }

    public static double CalculateRecencyFactor(DateTime? lastAttemptUtc, DateTime nowUtc)
    {
        if (lastAttemptUtc is null)
            return 0d;

        var days = Math.Max(0d, (nowUtc - lastAttemptUtc.Value).TotalDays);
        return Math.Exp(-days / 14d);
    }

    public static decimal CalculateWeaknessScore(decimal accuracy, int totalQuestions, double recencyFactor)
    {
        var attemptFactor = CalculateAttemptFactor(totalQuestions);
        var weakness = (1d - (double)accuracy) * attemptFactor * (0.5d + (0.5d * recencyFactor));
        return decimal.Round((decimal)Math.Clamp(weakness, 0d, 3d), 4, MidpointRounding.AwayFromZero);
    }

    public static decimal BoostWeaknessForSlowSolve(decimal weaknessScore, decimal accuracy, int? localP95Ms, int? userP95Ms)
    {
        if (!IsNearBoundary(accuracy) || !localP95Ms.HasValue || !userP95Ms.HasValue || userP95Ms.Value <= 0)
            return weaknessScore;

        if (localP95Ms.Value < userP95Ms.Value * 1.25d)
            return weaknessScore;

        var boosted = weaknessScore * 1.15m;
        return decimal.Round(Math.Clamp(boosted, 0m, 3m), 4, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateConfidence(int totalQuestions, double recencyFactor)
    {
        var quantity = 1d - Math.Exp(-totalQuestions / 10d);
        var confidence = quantity * (0.5d + (0.5d * recencyFactor));
        return decimal.Round((decimal)Math.Clamp(confidence, 0d, 1d), 4, MidpointRounding.AwayFromZero);
    }

    public static string MapWeaknessLevel(decimal accuracy)
    {
        if (accuracy >= 0.80m)
            return MathLearning.Domain.Entities.WeaknessLevels.Low;

        if (accuracy >= 0.60m)
            return MathLearning.Domain.Entities.WeaknessLevels.Medium;

        return MathLearning.Domain.Entities.WeaknessLevels.High;
    }

    public static bool IsNearBoundary(decimal accuracy) =>
        (accuracy >= 0.55m && accuracy <= 0.65m) ||
        (accuracy >= 0.75m && accuracy <= 0.85m);

    public static int? Percentile95(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
            return null;

        var ordered = values
            .Where(v => v >= 0)
            .OrderBy(v => v)
            .ToArray();

        if (ordered.Length == 0)
            return null;

        var rank = (int)Math.Ceiling(0.95d * ordered.Length) - 1;
        var index = Math.Clamp(rank, 0, ordered.Length - 1);
        return ordered[index];
    }
}
