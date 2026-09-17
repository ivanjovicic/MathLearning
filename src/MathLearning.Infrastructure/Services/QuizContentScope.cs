using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services;

/// <summary>
/// Resolves mobile content ids that may be either a subtopic id or a topic id.
/// </summary>
public sealed record QuizContentScope(
    int? SubtopicId,
    int? TopicId,
    bool ResolvedFromTopicId);

public static class QuizContentScopeResolver
{
    public static async Task<QuizContentScope?> ResolveAsync(
        ApiDbContext db,
        int contentId,
        CancellationToken cancellationToken = default)
    {
        if (contentId <= 0)
            return null;

        if (await db.Subtopics.AsNoTracking().AnyAsync(s => s.Id == contentId, cancellationToken))
            return new QuizContentScope(contentId, null, ResolvedFromTopicId: false);

        if (await db.Topics.AsNoTracking().AnyAsync(t => t.Id == contentId, cancellationToken))
            return new QuizContentScope(null, contentId, ResolvedFromTopicId: true);

        return null;
    }

    public static IQueryable<Domain.Entities.Question> ApplyScope(
        IQueryable<Domain.Entities.Question> query,
        QuizContentScope scope)
    {
        if (scope.SubtopicId is > 0)
            return query.Where(q => q.SubtopicId == scope.SubtopicId.Value);

        if (scope.TopicId is > 0)
            return query.Where(q => q.Subtopic != null && q.Subtopic.TopicId == scope.TopicId.Value);

        return query.Where(_ => false);
    }
}
