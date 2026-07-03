using MathLearning.Api.Services;
using MathLearning.Domain.Entities;

namespace MathLearning.Tests.Services;

public sealed class WeaknessScoringTests
{
    [Theory]
    [InlineData(0.90, WeaknessLevels.Low)]
    [InlineData(0.80, WeaknessLevels.Low)]
    [InlineData(0.7999, WeaknessLevels.Medium)]
    [InlineData(0.70, WeaknessLevels.Medium)]
    [InlineData(0.60, WeaknessLevels.Medium)]
    [InlineData(0.5999, WeaknessLevels.High)]
    [InlineData(0.0, WeaknessLevels.High)]
    public void MapWeaknessLevel_UsesExpectedThresholds(double accuracy, string expected)
    {
        Assert.Equal(expected, WeaknessScoring.MapWeaknessLevel((decimal)accuracy));
    }

    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(1, 0, 0.0)]
    [InlineData(1, -1, 0.0)]
    [InlineData(1, 3, 0.3333)]
    [InlineData(2, 3, 0.6667)]
    [InlineData(1, 6, 0.1667)]
    [InlineData(5, 8, 0.6250)]
    public void CalculateAccuracy_HandlesEmptyTotalsAndRoundsToFourDecimals(
        int correct,
        int total,
        double expected)
    {
        Assert.Equal((decimal)expected, WeaknessScoring.CalculateAccuracy(correct, total));
    }

    [Fact]
    public void CalculateAttemptFactor_IsMonotonicAndClamped()
    {
        var zero = WeaknessScoring.CalculateAttemptFactor(0);
        var nine = WeaknessScoring.CalculateAttemptFactor(9);
        var ninetyNine = WeaknessScoring.CalculateAttemptFactor(99);
        var huge = WeaknessScoring.CalculateAttemptFactor(int.MaxValue);

        Assert.Equal(0d, zero, precision: 10);
        Assert.Equal(1d, nine, precision: 10);
        Assert.Equal(1.5d, ninetyNine, precision: 10);
        Assert.Equal(1.5d, huge, precision: 10);
        Assert.True(zero < nine);
        Assert.True(nine < ninetyNine);
    }

    [Fact]
    public void CalculateRecencyFactor_NullAttemptHasNoRecencyWeight()
    {
        Assert.Equal(0d, WeaknessScoring.CalculateRecencyFactor(null, DateTime.UtcNow));
    }

    [Fact]
    public void CalculateRecencyFactor_CurrentAndFutureAttemptAreClampedToOne()
    {
        var now = new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(1d, WeaknessScoring.CalculateRecencyFactor(now, now), precision: 10);
        Assert.Equal(1d, WeaknessScoring.CalculateRecencyFactor(now.AddDays(5), now), precision: 10);
    }

    [Theory]
    [InlineData(14, 0.3678794412)]
    [InlineData(28, 0.1353352832)]
    [InlineData(140, 0.0000453999)]
    public void CalculateRecencyFactor_DecaysExponentially(int ageDays, double expected)
    {
        var now = new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);

        var actual = WeaknessScoring.CalculateRecencyFactor(now.AddDays(-ageDays), now);

        Assert.Equal(expected, actual, precision: 9);
    }

    [Fact]
    public void CalculateWeaknessScore_LowerAccuracyProducesHigherScore()
    {
        const double recency = 0.85d;

        var lowAccuracyScore = WeaknessScoring.CalculateWeaknessScore(0.45m, 30, recency);
        var highAccuracyScore = WeaknessScoring.CalculateWeaknessScore(0.90m, 30, recency);

        Assert.True(lowAccuracyScore > highAccuracyScore);
    }

    [Fact]
    public void CalculateWeaknessScore_MoreRecentAttemptProducesHigherActionableScore()
    {
        var stale = WeaknessScoring.CalculateWeaknessScore(0.50m, 20, recencyFactor: 0d);
        var recent = WeaknessScoring.CalculateWeaknessScore(0.50m, 20, recencyFactor: 1d);

        Assert.True(recent > stale);
    }

    [Theory]
    [InlineData(1.0, 1000, 1.0, 0.0)]
    [InlineData(2.0, 1000, 1.0, 0.0)]
    [InlineData(-10.0, 1000, 1.0, 3.0)]
    public void CalculateWeaknessScore_ClampsToSupportedRange(
        double accuracy,
        int total,
        double recency,
        double expected)
    {
        Assert.Equal(
            (decimal)expected,
            WeaknessScoring.CalculateWeaknessScore((decimal)accuracy, total, recency));
    }

    [Fact]
    public void CalculateConfidence_IsClampedToZeroOne()
    {
        var negative = WeaknessScoring.CalculateConfidence(-100, 1d);
        var low = WeaknessScoring.CalculateConfidence(0, 0d);
        var high = WeaknessScoring.CalculateConfidence(10_000, 1d);

        Assert.Equal(0m, negative);
        Assert.Equal(0m, low);
        Assert.Equal(1m, high);
    }

    [Fact]
    public void CalculateConfidence_IncreasesWithEvidenceAndRecency()
    {
        var lowEvidence = WeaknessScoring.CalculateConfidence(2, 0.2d);
        var moreEvidence = WeaknessScoring.CalculateConfidence(20, 0.2d);
        var recent = WeaknessScoring.CalculateConfidence(20, 1d);

        Assert.True(moreEvidence > lowEvidence);
        Assert.True(recent > moreEvidence);
    }

    [Theory]
    [InlineData(0.54, false)]
    [InlineData(0.55, true)]
    [InlineData(0.60, true)]
    [InlineData(0.65, true)]
    [InlineData(0.66, false)]
    [InlineData(0.74, false)]
    [InlineData(0.75, true)]
    [InlineData(0.80, true)]
    [InlineData(0.85, true)]
    [InlineData(0.86, false)]
    public void IsNearBoundary_UsesInclusiveThresholdWindows(double accuracy, bool expected)
    {
        Assert.Equal(expected, WeaknessScoring.IsNearBoundary((decimal)accuracy));
    }

    [Fact]
    public void BoostWeaknessForSlowSolve_OutsideAccuracyBoundaryDoesNotChangeScore()
    {
        var result = WeaknessScoring.BoostWeaknessForSlowSolve(
            weaknessScore: 0.5m,
            accuracy: 0.70m,
            localP95Ms: 2_000,
            userP95Ms: 1_000);

        Assert.Equal(0.5m, result);
    }

    [Theory]
    [InlineData(null, 1_000)]
    [InlineData(2_000, null)]
    [InlineData(2_000, 0)]
    [InlineData(2_000, -1)]
    public void BoostWeaknessForSlowSolve_MissingOrInvalidTimingDoesNotChangeScore(
        int? localP95Ms,
        int? userP95Ms)
    {
        var result = WeaknessScoring.BoostWeaknessForSlowSolve(
            weaknessScore: 0.5m,
            accuracy: 0.60m,
            localP95Ms,
            userP95Ms);

        Assert.Equal(0.5m, result);
    }

    [Theory]
    [InlineData(1_249, 1_000, 0.5)]
    [InlineData(1_250, 1_000, 0.575)]
    [InlineData(2_000, 1_000, 0.575)]
    public void BoostWeaknessForSlowSolve_UsesInclusiveTwentyFivePercentThreshold(
        int localP95Ms,
        int userP95Ms,
        double expected)
    {
        var result = WeaknessScoring.BoostWeaknessForSlowSolve(
            weaknessScore: 0.5m,
            accuracy: 0.60m,
            localP95Ms,
            userP95Ms);

        Assert.Equal((decimal)expected, result);
    }

    [Fact]
    public void BoostWeaknessForSlowSolve_ClampsBoostToThree()
    {
        var result = WeaknessScoring.BoostWeaknessForSlowSolve(
            weaknessScore: 2.9m,
            accuracy: 0.60m,
            localP95Ms: 2_000,
            userP95Ms: 1_000);

        Assert.Equal(3m, result);
    }

    [Fact]
    public void Percentile95_EmptyAndAllNegativeInputsReturnNull()
    {
        Assert.Null(WeaknessScoring.Percentile95(Array.Empty<int>()));
        Assert.Null(WeaknessScoring.Percentile95(new[] { -10, -1 }));
    }

    [Fact]
    public void Percentile95_IgnoresNegativeValuesAndDoesNotMutateInput()
    {
        var values = new[] { 100, -1, 500, 200, 300 };
        var original = values.ToArray();

        var percentile = WeaknessScoring.Percentile95(values);

        Assert.Equal(500, percentile);
        Assert.Equal(original, values);
    }

    [Fact]
    public void Percentile95_UsesNearestRankDefinition()
    {
        var values = Enumerable.Range(1, 20).ToArray();

        var percentile = WeaknessScoring.Percentile95(values);

        Assert.Equal(19, percentile);
    }

    [Fact]
    public void Percentile95_SingleValueReturnsThatValue()
    {
        Assert.Equal(321, WeaknessScoring.Percentile95(new[] { 321 }));
    }
}
