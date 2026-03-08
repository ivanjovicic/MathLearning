using System.Security.Cryptography;
using System.Text;
using MathLearning.Application.DTOs.Sync;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MathLearning.Infrastructure.Services.Sync;

public sealed class OfflineBundleService : IOfflineBundleService
{
    private readonly ApiDbContext db;
    private readonly IOptions<SyncOptions> options;

    public OfflineBundleService(ApiDbContext db, IOptions<SyncOptions> options)
    {
        this.db = db;
        this.options = options;
    }

    public async Task<OfflineBundleResponseDto> GetBundleAsync(
        string userId,
        int? subtopicId,
        int questionCount,
        CancellationToken cancellationToken)
    {
        var effectiveCount = questionCount > 0
            ? Math.Min(questionCount, options.Value.DefaultQuestionBundleSize)
            : options.Value.DefaultQuestionBundleSize;

        IQueryable<Question> query = db.Questions
            .AsNoTracking()
            .Include(x => x.Options);

        if (subtopicId.HasValue)
        {
            query = query.Where(x => x.SubtopicId == subtopicId.Value);
        }

        var questions = await query
            .OrderBy(x => x.Difficulty)
            .ThenBy(x => x.Id)
            .Take(effectiveCount)
            .ToListAsync(cancellationToken);

        var questionIds = questions.Select(x => x.Id).ToList();
        var subtopicIds = questions.Select(x => x.SubtopicId).Distinct().ToList();

        var subtopics = await db.Subtopics
            .AsNoTracking()
            .Where(x => subtopicIds.Contains(x.Id))
            .OrderBy(x => x.TopicId)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var topicIds = subtopics.Select(x => x.TopicId).Distinct().ToList();
        var topics = await db.Topics
            .AsNoTracking()
            .Where(x => topicIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var userStats = await db.UserQuestionStats
            .AsNoTracking()
            .Where(x => x.UserId == userId && questionIds.Contains(x.QuestionId))
            .ToListAsync(cancellationToken);

        var reviewStats = await db.QuestionStats
            .AsNoTracking()
            .Where(x => x.UserId == userId && questionIds.Contains(x.QuestionId))
            .ToDictionaryAsync(x => x.QuestionId, cancellationToken);

        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        var manifestVersion = ComputeBundleVersion(questions, topics, subtopics, profile?.UpdatedAt);

        return new OfflineBundleResponseDto(
            new OfflineBundleManifestDto(
                manifestVersion,
                DateTime.UtcNow,
                questions.Count,
                topics.Count,
                subtopics.Count),
            questions.Select(q => new SyncBundleQuestionDto(
                q.Id,
                q.Type,
                q.Text,
                q.Difficulty,
                q.Options
                    .OrderBy(o => o.Id)
                    .Select(o => new SyncBundleOptionDto(o.Id, o.Text))
                    .ToList(),
                q.HintClue,
                q.HintFormula,
                q.Explanation,
                q.Explanation))
                .ToList(),
            topics.Select(x => new OfflineBundleTopicDto(x.Id, x.Name, x.Description)).ToList(),
            subtopics.Select(x => new OfflineBundleSubtopicDto(x.Id, x.TopicId, x.Name)).ToList(),
            questions.Select(x => x.Id).ToList(),
            new OfflineBundleUserSnapshotDto(
                profile?.Xp ?? 0,
                profile?.Level ?? 1,
                profile?.Streak ?? 0,
                userStats.Select(x => new OfflineBundleQuestionProgressDto(
                    x.QuestionId,
                    x.Attempts,
                    x.CorrectAttempts,
                    x.LastAttemptAt,
                    reviewStats.TryGetValue(x.QuestionId, out var review) ? review.NextReview : null))
                    .ToList()));
    }

    private static string ComputeBundleVersion(
        IReadOnlyList<Question> questions,
        IReadOnlyList<Topic> topics,
        IReadOnlyList<Subtopic> subtopics,
        DateTime? profileUpdatedAt)
    {
        var builder = new StringBuilder();
        builder.Append(profileUpdatedAt?.ToUniversalTime().Ticks ?? 0);

        foreach (var topic in topics.OrderBy(x => x.Id))
        {
            builder.Append('|').Append(topic.Id).Append(':').Append(topic.Name);
        }

        foreach (var subtopic in subtopics.OrderBy(x => x.Id))
        {
            builder.Append('|').Append(subtopic.Id).Append(':').Append(subtopic.TopicId).Append(':').Append(subtopic.Name);
        }

        foreach (var question in questions.OrderBy(x => x.Id))
        {
            builder.Append('|').Append(question.Id).Append(':').Append(question.Difficulty).Append(':').Append(question.Type);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
