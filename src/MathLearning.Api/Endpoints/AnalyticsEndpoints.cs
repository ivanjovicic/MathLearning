using MathLearning.Application.DTOs.Analytics;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;

namespace MathLearning.Api.Endpoints;

public static class AnalyticsEndpoints
{
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
            if (!TryGetAppUserId(ctx, out var appUserId))
                return Results.Unauthorized();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var userId = UserIdGuidMapper.FromAppUserId(appUserId);
            var take = page * pageSize;
            var all = await service.GetWeakTopicsAsync(userId, take, ct);
            var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

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
                page,
                pageSize,
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
            if (!TryGetAppUserId(ctx, out var appUserId))
                return Results.Unauthorized();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var take = page * pageSize;
            var skip = (page - 1) * pageSize;

            var userId = UserIdGuidMapper.FromAppUserId(appUserId);
            var topics = (await service.GetWeakTopicsAsync(userId, take, ct))
                .Skip(skip)
                .Take(pageSize)
                .ToList();
            var subtopics = (await service.GetWeakSubtopicsAsync(userId, take, ct))
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            return Results.Ok(new
            {
                weakTopics = topics.Select(MapTopicDetail),
                weakSubtopics = subtopics.Select(MapSubtopicDetail),
                page,
                pageSize,
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
            if (!TryGetAppUserId(ctx, out var appUserId))
                return Results.Unauthorized();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var take = page * pageSize;
            var skip = (page - 1) * pageSize;

            var userId = UserIdGuidMapper.FromAppUserId(appUserId);
            var recommendationsRows = (await service.GeneratePracticeRecommendationsAsync(userId, take, ct))
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            return Results.Ok(new PracticeRecommendationsResponse(
                Recommendations: recommendationsRows,
                Page: page,
                PageSize: pageSize,
                Returned: recommendationsRows.Count));
        });
    }

    private static bool TryGetAppUserId(HttpContext ctx, out int appUserId)
    {
        appUserId = 0;
        var raw = ctx.User.FindFirst("userId")?.Value;
        return int.TryParse(raw, out appUserId);
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
