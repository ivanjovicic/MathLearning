using MathLearning.Application.Services;
using Microsoft.Extensions.Caching.Memory;

namespace MathLearning.Api.Services;

public sealed class BktService : IBktService
{
    private static readonly TimeSpan TopicParamCacheTtl = TimeSpan.FromHours(6);
    private readonly IMemoryCache _cache;

    public BktService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public BktParameters GetParamsForTopic(int topicId)
    {
        var cacheKey = $"bkt:params:{topicId}";
        if (_cache.TryGetValue(cacheKey, out BktParameters? cached) && cached is not null)
            return cached;

        // Defaults; replace with topic-specific tuning when available.
        var parameters = new BktParameters(
            PL0: 0.20m,
            PT: 0.12m,
            PG: 0.20m,
            PS: 0.10m);

        _cache.Set(cacheKey, parameters, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TopicParamCacheTtl,
            Size = 1
        });

        return parameters;
    }

    public decimal UpdateMastery(decimal prior, bool isCorrect, BktParameters parameters)
    {
        prior = Math.Clamp(prior, 0.0001m, 0.9999m);
        var pT = Math.Clamp(parameters.PT, 0.0001m, 0.9999m);
        var pG = Math.Clamp(parameters.PG, 0.0001m, 0.9999m);
        var pS = Math.Clamp(parameters.PS, 0.0001m, 0.9999m);

        decimal posterior;
        if (isCorrect)
        {
            var numerator = prior * (1 - pS);
            var denominator = numerator + ((1 - prior) * pG);
            posterior = denominator <= 0 ? prior : numerator / denominator;
        }
        else
        {
            var numerator = prior * pS;
            var denominator = numerator + ((1 - prior) * (1 - pG));
            posterior = denominator <= 0 ? prior : numerator / denominator;
        }

        var transitioned = posterior + ((1 - posterior) * pT);
        return decimal.Round(Math.Clamp(transitioned, 0m, 1m), 4, MidpointRounding.AwayFromZero);
    }
}
