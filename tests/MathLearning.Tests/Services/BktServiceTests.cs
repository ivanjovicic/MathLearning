using MathLearning.Api.Services;
using MathLearning.Application.Services;
using Microsoft.Extensions.Caching.Memory;

namespace MathLearning.Tests.Services;

public sealed class BktServiceTests
{
    [Fact]
    public void GetParamsForTopic_OnCacheMiss_ReturnsDocumentedDefaults()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new BktService(cache);

        var parameters = sut.GetParamsForTopic(17);

        Assert.Equal(0.20m, parameters.PL0);
        Assert.Equal(0.12m, parameters.PT);
        Assert.Equal(0.20m, parameters.PG);
        Assert.Equal(0.10m, parameters.PS);
    }

    [Fact]
    public void GetParamsForTopic_RepeatedCall_ReturnsSameCachedInstance()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new BktService(cache);

        var first = sut.GetParamsForTopic(5);
        var second = sut.GetParamsForTopic(5);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetParamsForTopic_WithPreloadedCache_ReturnsCachedParameters()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var cached = new BktParameters(PL0: 0.33m, PT: 0.22m, PG: 0.11m, PS: 0.05m);
        cache.Set("bkt:params:99", cached);
        var sut = new BktService(cache);

        var result = sut.GetParamsForTopic(99);

        Assert.Same(cached, result);
    }

    [Fact]
    public void GetParamsForTopic_DifferentTopics_UseDifferentCacheEntries()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new BktService(cache);

        var first = sut.GetParamsForTopic(1);
        var second = sut.GetParamsForTopic(2);

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
    }

    [Fact]
    public void UpdateMastery_CorrectAnswer_ReturnsExpectedRoundedProbability()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new BktService(cache);
        var parameters = sut.GetParamsForTopic(1);

        var updated = sut.UpdateMastery(0.20m, isCorrect: true, parameters);

        Assert.Equal(0.5859m, updated);
        Assert.True(updated > 0.20m);
    }

    [Fact]
    public void UpdateMastery_IncorrectAnswer_ReturnsExpectedRoundedProbability()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new BktService(cache);
        var parameters = sut.GetParamsForTopic(1);

        var updated = sut.UpdateMastery(0.85m, isCorrect: false, parameters);

        Assert.Equal(0.4849m, updated);
        Assert.True(updated < 0.85m);
    }

    [Theory]
    [InlineData("-10", false)]
    [InlineData("-10", true)]
    [InlineData("10", false)]
    [InlineData("10", true)]
    public void UpdateMastery_ExtremePrior_IsClampedToProbabilityRange(
        string priorText,
        bool isCorrect)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new BktService(cache);
        var parameters = sut.GetParamsForTopic(1);
        var prior = decimal.Parse(priorText, System.Globalization.CultureInfo.InvariantCulture);

        var updated = sut.UpdateMastery(prior, isCorrect, parameters);

        Assert.InRange(updated, 0m, 1m);
    }

    [Fact]
    public void UpdateMastery_ExtremeParameters_AreClampedBeforeCalculation()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new BktService(cache);
        var parameters = new BktParameters(
            PL0: -20m,
            PT: 20m,
            PG: -20m,
            PS: 20m);

        var correct = sut.UpdateMastery(0.5m, isCorrect: true, parameters);
        var incorrect = sut.UpdateMastery(0.5m, isCorrect: false, parameters);

        Assert.InRange(correct, 0m, 1m);
        Assert.InRange(incorrect, 0m, 1m);
    }
}
