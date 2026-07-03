using MathLearning.Application.DTOs.Analytics;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;

namespace MathLearning.Api.Endpoints;

public static class AnalyticsEndpoints
{
    private const int MaxAnalyticsPage = 100;

    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var analytics = app.MapGroup("/api/analytics")
            .RequireAuthorization()
            .WithTags("Analytics");

        analytics.MapGet("/weakness", async (
            IWeaknessAnalysisService service,
            HttpContext ctx,
            int page = 1,
            int pageSize = 5,
            CancellationToken ct = default) =>
        {
            if (!TryGetAnalyticsUserId(ctx, out var userId))
                return Results.Unauthorized();

            var paging = PaginationBounds.Normalize(
                page,
                Math.Clamp(pageSize, 1, 50),
                defaultPageSize: 5,
                maxPageSize: 50,
                maxPage: MaxAnalyticsPage);
            var all = await service.GetWeakTopicsAsync(userId, paging.FetchCount, ct);
            var items = all.Skip(paging.Skip).Take(paging.PageSize).ToList();

            return Results.Ok(new
            {
                weakTopics = items.Select(x => new
                {
                    topicId = x.TopicId,
                    topicName = x.TopicName,
                    accuracy = x.Accuracy,
                    weaknessLevel = x.WeaknessLevel,
                    confidence = x.Confidence
                }),
                page = paging.Page,
                pageSize = paging.PageSize,
                returned = items.Count
            });
        });

        analytics.MapGet("/weakness/details", async (
            IWeaknessAnalysisService service,
            HttpContext ctx,
            int page = 1,
            int pageSize = 10,
            CancellationToken ct = default) =>
        {
            if (!TryGetAnalyticsUserId(ctx, out var userId))
                return Results.Unauthorized();

            var paging = PaginationBounds.Normalize(
                page,
                Math.Clamp(pageSize, 1, 100),
                defaultPageSize: 10,
                maxPageSize: 100,
                maxPage: MaxAnalyticsPage);

            var topics = (await service.GetWeakTopicsAsync(userId, paging.FetchCount, ct))
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .ToList();
            var subtopics = (await service.GetWeakSubtopicsAsync(userId, paging.FetchCount, ct))
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .ToList();

            return Results.Ok(new
            {
                weakTopics = topics.Select(MapTopicDetail),
                weakSubtopics = subtopics.Select(MapSubtopicDetail),
                page = paging.Page,
                pageSize = paging.PageSize,
                returnedTopics = topics.Count,
                returnedSubtopics = subtopics.Count
            });
        });

        var recommendations = app.MapGroup("/api/recommendations")
            .RequireAuthorization()
            .WithTags("Recommendations");

        recommendations.MapGet("/practice", async (
            IWeaknessAnalysisService service,
            HttpContext ctx,
            int page = 1,
            int pageSize = 10,
            CancellationToken ct = default) =>
        {
            if (!TryGetAnalyticsUserId(ctx, out var userId))
                return Results.Unauthorized();

            var paging = PaginationBounds.Normalize(
                page,
                Math.Clamp(pageSize, 1, 100),
                defaultPageSize: 10,
                maxPageSize: 100,
                maxPage: MaxAnalyticsPage);
            var recommendationRows = (await service.GeneratePracticeRecommendationsAsync(
                    userId,
                    paging.FetchCount,
                    ct))
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .ToList();

            return Results.Ok(new PracticeRecommendationsResponse(
                Recommendations: recommendationRows,
                Page: paging.Page,
                PageSize: paging.PageSize,
                Returned: recommendationRows.Count));
        });
    }

    private static bool TryGetAnalyticsUserId(HttpContext ctx, out Guid analyticsUserId)
    {
        analyticsUserId = Guid.Empty;
        var raw = ctx.User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        analyticsUserId = UserIdGuidMapper.FromIdentityUserId(raw);
        return analyticsUserId != Guid.Empty;
    }

    private static object MapTopicDetail(WeakTopicDto dto) => new
    {
        topicId = dto.TopicId,
        topicName = dto.TopicName,
        accuracy = dto.Accuracy,
        weaknessLevel = dto.WeaknessLevel,
        confidence = dto.Confidence,
        weaknessScore = dto.WeaknessScore,
        lastAttempt = dto.LastAttempt,
        totalQuestions = dto.TotalQuestions
    };

    private static object MapSubtopicDetail(WeakSubtopicDto dto) => new
    {
        subtopicId = dto.SubtopicId,
        subtopicName = dto.SubtopicName,
        topicId = dto.TopicId,
        topicName = dto.TopicName,
        accuracy = dto.Accuracy,
        weaknessLevel = dto.WeaknessLevel,
        confidence = dto.Confidence,
        weaknessScore = dto.WeaknessScore,
        lastAttempt = dto.LastAttempt,
        totalQuestions = dto.TotalQuestions
    };
}
