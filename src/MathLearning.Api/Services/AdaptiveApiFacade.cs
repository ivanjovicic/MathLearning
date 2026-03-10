using MathLearning.Application.DTOs.Adaptive;
using MathLearning.Application.DTOs.Common;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;

namespace MathLearning.Api.Services;

public sealed class AdaptiveApiFacade
{
    private static readonly TimeSpan PathFreshTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PathStaleTtl = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan RecommendationsFreshTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RecommendationsStaleTtl = TimeSpan.FromMinutes(20);

    private readonly IAdaptiveLearningService _adaptiveLearningService;
    private readonly InMemoryCacheService _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAdaptiveAnalyticsService _analytics;
    private readonly ILogger<AdaptiveApiFacade> _logger;

    public AdaptiveApiFacade(
        IAdaptiveLearningService adaptiveLearningService,
        InMemoryCacheService cache,
        IServiceScopeFactory scopeFactory,
        IAdaptiveAnalyticsService analytics,
        ILogger<AdaptiveApiFacade> logger)
    {
        _adaptiveLearningService = adaptiveLearningService;
        _cache = cache;
        _scopeFactory = scopeFactory;
        _analytics = analytics;
        _logger = logger;
    }

    public async Task<ApiResult<AdaptiveSessionDto>> StartAdaptiveSessionAsync(string userId, CancellationToken ct)
    {
        try
        {
            var session = await RetryPolicy.ExecuteAsync(
                token => _adaptiveLearningService.GeneratePracticeSessionAsync(userId),
                _logger,
                "start_adaptive_session",
                ct);

            await InvalidateAdaptiveCachesAsync(userId);
            _analytics.TrackEvent("adaptive_practice_started", userId, new
            {
                sessionId = session.Id,
                itemCount = session.Items.Count
            });

            return ApiResult<AdaptiveSessionDto>.Ok(MapSession(session));
        }
        catch (Exception ex)
        {
            return BuildFailureResult<AdaptiveSessionDto>(ex, "Failed to start adaptive session.");
        }
    }

    public async Task<ApiResult<AdaptiveAnswerResult>> SubmitAdaptiveSessionAnswerAsync(
        string userId,
        AdaptiveAnswerRequest request,
        CancellationToken ct)
    {
        try
        {
            var answerResult = await RetryPolicy.ExecuteAsync(
                token => _adaptiveLearningService.SubmitAnswerAsync(userId, request),
                _logger,
                "submit_adaptive_session_answer",
                ct);

            await InvalidateAdaptiveCachesAsync(userId);
            _analytics.TrackEvent("adaptive_answer_submitted", userId, new
            {
                request.AdaptiveSessionId,
                request.AdaptiveSessionItemId,
                request.QuestionId,
                answerResult.IsCorrect,
                answerResult.DifficultyLevel
            });

            return ApiResult<AdaptiveAnswerResult>.Ok(answerResult);
        }
        catch (Exception ex)
        {
            return BuildFailureResult<AdaptiveAnswerResult>(ex, "Failed to submit adaptive session answer.");
        }
    }

    public async Task<ApiResult<AdaptivePathResponse>> GetAdaptivePathAsync(string userId, CancellationToken ct)
    {
        var cacheKey = GetAdaptivePathCacheKey(userId);
        var cached = await _cache.GetAsync<CachedPayload<AdaptivePathPayload>>(cacheKey);
        if (cached is not null && IsWithin(cached.CachedAtUtc, PathFreshTtl))
        {
            _analytics.TrackEvent("adaptive_path_loaded", userId, new { source = "cache_fresh" });
            return ApiResult<AdaptivePathResponse>.Ok(
                CreatePathResponse(cached, servedFromCache: true, isStale: false, fallbackReason: null));
        }

        if (cached is not null && IsWithin(cached.CachedAtUtc, PathStaleTtl))
        {
            _analytics.TrackEvent("adaptive_path_loaded", userId, new { source = "cache_stale" });
            TriggerBackgroundPathRefresh(userId);
            return ApiResult<AdaptivePathResponse>.Ok(
                CreatePathResponse(cached, servedFromCache: true, isStale: true, fallbackReason: "stale_while_revalidate"));
        }

        try
        {
            var payload = await RetryPolicy.ExecuteAsync(
                token => BuildAdaptivePathPayloadAsync(userId, token),
                _logger,
                "get_adaptive_path",
                ct);

            var wrapped = new CachedPayload<AdaptivePathPayload>(payload, DateTime.UtcNow);
            await _cache.SetAsync(cacheKey, wrapped, PathStaleTtl);
            _analytics.TrackEvent("adaptive_path_loaded", userId, new { source = "origin" });

            return ApiResult<AdaptivePathResponse>.Ok(
                CreatePathResponse(wrapped, servedFromCache: false, isStale: false, fallbackReason: null));
        }
        catch (Exception ex)
        {
            if (cached is not null)
            {
                _logger.LogWarning(ex, "Serving stale adaptive path due to fetch failure. UserId={UserId}", userId);
                _analytics.TrackEvent("adaptive_path_loaded", userId, new { source = "cache_fallback_on_error" });
                return ApiResult<AdaptivePathResponse>.Ok(
                    CreatePathResponse(cached, servedFromCache: true, isStale: true, fallbackReason: "origin_fetch_failed"));
            }

            return BuildFailureResult<AdaptivePathResponse>(ex, "Failed to load adaptive path.");
        }
    }

    public async Task<ApiResult<AdaptiveRecommendationsResponse>> GetAdaptiveRecommendationsAsync(string userId, CancellationToken ct)
    {
        var cacheKey = GetAdaptiveRecommendationsCacheKey(userId);
        var cached = await _cache.GetAsync<CachedPayload<AdaptiveRecommendationsPayload>>(cacheKey);
        if (cached is not null && IsWithin(cached.CachedAtUtc, RecommendationsFreshTtl))
        {
            _analytics.TrackEvent("adaptive_recommendations_loaded", userId, new { source = "cache_fresh" });
            return ApiResult<AdaptiveRecommendationsResponse>.Ok(
                CreateRecommendationsResponse(cached, servedFromCache: true, isStale: false, fallbackReason: null));
        }

        if (cached is not null && IsWithin(cached.CachedAtUtc, RecommendationsStaleTtl))
        {
            _analytics.TrackEvent("adaptive_recommendations_loaded", userId, new { source = "cache_stale" });
            TriggerBackgroundRecommendationsRefresh(userId);
            return ApiResult<AdaptiveRecommendationsResponse>.Ok(
                CreateRecommendationsResponse(cached, servedFromCache: true, isStale: true, fallbackReason: "stale_while_revalidate"));
        }

        try
        {
            var recommendations = await RetryPolicy.ExecuteAsync(
                token => _adaptiveLearningService.GetRecommendationsAsync(userId),
                _logger,
                "get_adaptive_recommendations",
                ct);

            var wrapped = new CachedPayload<AdaptiveRecommendationsPayload>(
                new AdaptiveRecommendationsPayload(recommendations, DateTime.UtcNow),
                DateTime.UtcNow);

            await _cache.SetAsync(cacheKey, wrapped, RecommendationsStaleTtl);
            _analytics.TrackEvent("adaptive_recommendations_loaded", userId, new { source = "origin" });

            return ApiResult<AdaptiveRecommendationsResponse>.Ok(
                CreateRecommendationsResponse(wrapped, servedFromCache: false, isStale: false, fallbackReason: null));
        }
        catch (Exception ex)
        {
            if (cached is not null)
            {
                _logger.LogWarning(ex, "Serving stale adaptive recommendations due to fetch failure. UserId={UserId}", userId);
                _analytics.TrackEvent("adaptive_recommendations_loaded", userId, new { source = "cache_fallback_on_error" });
                return ApiResult<AdaptiveRecommendationsResponse>.Ok(
                    CreateRecommendationsResponse(cached, servedFromCache: true, isStale: true, fallbackReason: "origin_fetch_failed"));
            }

            return BuildFailureResult<AdaptiveRecommendationsResponse>(ex, "Failed to load adaptive recommendations.");
        }
    }

    public async Task<ApiResult<List<ReviewItem>>> GetDueReviewsAsync(string userId, CancellationToken ct)
    {
        try
        {
            var reviews = await RetryPolicy.ExecuteAsync(
                token => _adaptiveLearningService.GetDueReviewsAsync(userId),
                _logger,
                "get_due_reviews",
                ct);

            return ApiResult<List<ReviewItem>>.Ok(reviews);
        }
        catch (Exception ex)
        {
            return BuildFailureResult<List<ReviewItem>>(ex, "Failed to load due review items.");
        }
    }

    private async Task<AdaptivePathPayload> BuildAdaptivePathPayloadAsync(string userId, CancellationToken ct)
    {
        var recommendations = await _adaptiveLearningService.GetRecommendationsAsync(userId);
        var dueReviews = await _adaptiveLearningService.GetDueReviewsAsync(userId);

        return new AdaptivePathPayload(recommendations, dueReviews, DateTime.UtcNow);
    }

    private static AdaptiveSessionDto MapSession(AdaptiveSession session)
    {
        var items = session.Items
            .OrderBy(i => i.Sequence)
            .Select(i => new AdaptiveSessionItemDto(
                i.Id,
                i.QuestionId,
                i.TopicId,
                i.SubtopicId,
                i.SourceType,
                i.DifficultyLevel,
                i.Sequence))
            .ToList();

        return new AdaptiveSessionDto(
            session.Id,
            session.CreatedAt,
            session.ExpiresAt,
            session.ProfileDifficulty,
            items);
    }

    private static AdaptivePathResponse CreatePathResponse(
        CachedPayload<AdaptivePathPayload> cached,
        bool servedFromCache,
        bool isStale,
        string? fallbackReason) =>
        new(
            cached.Payload,
            ServedFromCache: servedFromCache,
            IsStale: isStale,
            FallbackReason: fallbackReason,
            CachedAtUtc: cached.CachedAtUtc);

    private static AdaptiveRecommendationsResponse CreateRecommendationsResponse(
        CachedPayload<AdaptiveRecommendationsPayload> cached,
        bool servedFromCache,
        bool isStale,
        string? fallbackReason) =>
        new(
            cached.Payload,
            ServedFromCache: servedFromCache,
            IsStale: isStale,
            FallbackReason: fallbackReason,
            CachedAtUtc: cached.CachedAtUtc);

    private static bool IsWithin(DateTime cachedAtUtc, TimeSpan ttl) =>
        DateTime.UtcNow - cachedAtUtc <= ttl;

    private async Task InvalidateAdaptiveCachesAsync(string userId)
    {
        await _cache.RemoveAsync(GetAdaptivePathCacheKey(userId));
        await _cache.RemoveAsync(GetAdaptiveRecommendationsCacheKey(userId));
    }

    private void TriggerBackgroundPathRefresh(string userId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var adaptiveLearningService = scope.ServiceProvider.GetRequiredService<IAdaptiveLearningService>();
                var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheService>();
                var payload = await BuildAdaptivePathPayloadForRefreshAsync(userId, adaptiveLearningService);
                await cache.SetAsync(
                    GetAdaptivePathCacheKey(userId),
                    new CachedPayload<AdaptivePathPayload>(payload, DateTime.UtcNow),
                    PathStaleTtl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background adaptive path refresh failed. UserId={UserId}", userId);
            }
        });
    }

    private void TriggerBackgroundRecommendationsRefresh(string userId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var adaptiveLearningService = scope.ServiceProvider.GetRequiredService<IAdaptiveLearningService>();
                var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheService>();
                var recommendations = await adaptiveLearningService.GetRecommendationsAsync(userId);
                var payload = new AdaptiveRecommendationsPayload(recommendations, DateTime.UtcNow);

                await cache.SetAsync(
                    GetAdaptiveRecommendationsCacheKey(userId),
                    new CachedPayload<AdaptiveRecommendationsPayload>(payload, DateTime.UtcNow),
                    RecommendationsStaleTtl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background adaptive recommendations refresh failed. UserId={UserId}", userId);
            }
        });
    }

    private static async Task<AdaptivePathPayload> BuildAdaptivePathPayloadForRefreshAsync(
        string userId,
        IAdaptiveLearningService adaptiveLearningService)
    {
        var recommendations = await adaptiveLearningService.GetRecommendationsAsync(userId);
        var dueReviews = await adaptiveLearningService.GetDueReviewsAsync(userId);

        return new AdaptivePathPayload(recommendations, dueReviews, DateTime.UtcNow);
    }

    private static ApiResult<T> BuildFailureResult<T>(Exception ex, string defaultMessage)
    {
        var traceId = Guid.NewGuid().ToString("N");
        var retryAfterSeconds = ResolveRetryAfterSeconds(ex);

        return ex switch
        {
            ArgumentException => ApiResult<T>.Fail(
                error: ex.Message,
                errorCode: "VALIDATION_ERROR",
                errorDetails: BuildErrorDetails(ex),
                traceId: traceId),

            KeyNotFoundException => ApiResult<T>.Fail(
                error: ex.Message,
                errorCode: "NOT_FOUND",
                errorDetails: BuildErrorDetails(ex),
                traceId: traceId),

            RateLimitedOperationException => ApiResult<T>.RateLimited(
                error: ex.Message,
                errorDetails: BuildErrorDetails(ex),
                traceId: traceId,
                retryAfterSeconds: retryAfterSeconds),

            _ => ApiResult<T>.Fail(
                error: defaultMessage,
                errorCode: "INTERNAL_ERROR",
                errorDetails: BuildErrorDetails(ex),
                traceId: traceId,
                isRateLimited: retryAfterSeconds.HasValue,
                retryAfterSeconds: retryAfterSeconds)
        };
    }

    private static int? ResolveRetryAfterSeconds(Exception ex)
    {
        if (ex is RateLimitedOperationException rateLimited)
            return rateLimited.RetryAfterSeconds;

        if (ex.Data.Contains("Retry-After"))
            return RetryAfterParser.ParseRetryAfterSeconds(ex.Data["Retry-After"]?.ToString());

        if (ex.Data.Contains("retry-after"))
            return RetryAfterParser.ParseRetryAfterSeconds(ex.Data["retry-after"]?.ToString());

        return null;
    }

    private static object BuildErrorDetails(Exception ex) =>
        new
        {
            exceptionType = ex.GetType().Name,
            message = ex.Message
        };

    private static string GetAdaptivePathCacheKey(string userId) =>
        $"adaptive-api:path:{userId}";

    private static string GetAdaptiveRecommendationsCacheKey(string userId) =>
        $"adaptive-api:recommendations:{userId}";

    private sealed record CachedPayload<T>(T Payload, DateTime CachedAtUtc);
}
