using MathLearning.Api.Services;
using Microsoft.Extensions.Caching.Memory;

namespace MathLearning.Tests.Services;

public class BktServiceTests
{
    [Fact]
    public void UpdateMastery_CorrectAnswer_IncreasesProbability()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new BktService(cache);
        var p = sut.GetParamsForTopic(1);

        var updated = sut.UpdateMastery(0.20m, isCorrect: true, p);

        Assert.True(updated > 0.20m);
        Assert.InRange(updated, 0m, 1m);
    }

    [Fact]
    public void UpdateMastery_IncorrectAnswer_DecreasesFromHighPrior()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new BktService(cache);
        var p = sut.GetParamsForTopic(1);

        var updated = sut.UpdateMastery(0.85m, isCorrect: false, p);

        Assert.True(updated < 0.85m);
        Assert.InRange(updated, 0m, 1m);
    }

    [Fact]
    public void UpdateMastery_AlwaysClampedToZeroOne()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new BktService(cache);
        var p = sut.GetParamsForTopic(1);

        var low = sut.UpdateMastery(-10m, isCorrect: false, p);
        var high = sut.UpdateMastery(10m, isCorrect: true, p);

        Assert.InRange(low, 0m, 1m);
        Assert.InRange(high, 0m, 1m);
    }
}
