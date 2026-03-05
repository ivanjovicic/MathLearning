using MathLearning.Api.Services;
using MathLearning.Domain.Entities;

namespace MathLearning.Tests.Services;

public class WeaknessScoringTests
{
    [Theory]
    [InlineData(0.90, WeaknessLevels.Low)]
    [InlineData(0.80, WeaknessLevels.Low)]
    [InlineData(0.70, WeaknessLevels.Medium)]
    [InlineData(0.60, WeaknessLevels.Medium)]
    [InlineData(0.59, WeaknessLevels.High)]
    public void MapWeaknessLevel_UsesExpectedThresholds(double accuracy, string expected)
    {
        var level = WeaknessScoring.MapWeaknessLevel((decimal)accuracy);
        Assert.Equal(expected, level);
    }

    [Fact]
    public void CalculateWeaknessScore_LowerAccuracy_ProducesHigherScore()
    {
        var recency = 0.85d;
        var lowAccuracyScore = WeaknessScoring.CalculateWeaknessScore(0.45m, 30, recency);
        var highAccuracyScore = WeaknessScoring.CalculateWeaknessScore(0.90m, 30, recency);

        Assert.True(lowAccuracyScore > highAccuracyScore);
    }

    [Fact]
    public void CalculateConfidence_IsClampedToZeroOne()
    {
        var low = WeaknessScoring.CalculateConfidence(0, 0d);
        var high = WeaknessScoring.CalculateConfidence(10_000, 1d);

        Assert.InRange(low, 0m, 1m);
        Assert.InRange(high, 0m, 1m);
        Assert.True(high > low);
    }
}
