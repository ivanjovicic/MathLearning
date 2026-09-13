using System.Security.Cryptography;
using System.Text;
using MathLearning.Application.DTOs.Sync;
using MathLearning.Application.Helpers;
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
        string? acceptLanguage,
        CancellationToken cancellationToken)
    {
        var effectiveCount = questionCount > 0
            ? Math.Min(questionCount, options.Value.DefaultQuestionBundleSize)
            : options.Value.DefaultQuestionBundleSize;

        var settings = await db.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        var userLanguage = TranslationHelper.ResolveLanguage(settings?.Language, acceptLanguage);

        IQueryable<Question> query = db.Questions
            .AsNoTracking()
            .Include(x => x.Options).ThenInclude(o => o.Translations)
            .Include(x => x.Translations)
            .Include(x => x.Steps).ThenInclude(s => s.Translations);

        query = query.Where(x =>
            x.PublishState == QuestionPublishStates.Published &&
            !x.IsDeleted);

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

        var contentVersion = ComputeContentVersion(questions, topics, subtopics, userLanguage);
        var snapshotVersion = ComputeSnapshotVersion(profile, userStats, reviewStats);

        return new OfflineBundleResponseDto(
            new OfflineBundleManifestDto(
                contentVersion,
                snapshotVersion,
                DateTime.UtcNow,
                questions.Count,
                topics.Count,
                subtopics.Count),
            questions.Select(q => new SyncBundleQuestionDto(
                q.Id,
                q.Type,
                TranslationHelper.GetText(q, userLanguage),
                q.Difficulty,
                q.Options
                    .OrderBy(o => o.Order)
                    .ThenBy(o => o.Id)
                    .Select(o => new SyncBundleOptionDto(
                        o.Id,
                        TranslationHelper.GetOptionText(o, userLanguage),
                        o.TextFormat,
                        o.RenderMode,
                        TranslationHelper.GetOptionSemanticsAltText(o, userLanguage)))
                    .ToList(),
                TranslationHelper.GetHintLight(q, userLanguage),
                TranslationHelper.GetHintMedium(q, userLanguage),
                TranslationHelper.GetHintFull(q, userLanguage),
                TranslationHelper.GetExplanation(q, userLanguage),
                q.TextFormat,
                q.ExplanationFormat,
                q.HintFormat,
                q.TextRenderMode,
                q.ExplanationRenderMode,
                q.HintRenderMode,
                TranslationHelper.GetQuestionSemanticsAltText(q, userLanguage)))
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

    private static string ComputeContentVersion(
        IReadOnlyList<Question> questions,
        IReadOnlyList<Topic> topics,
        IReadOnlyList<Subtopic> subtopics,
        string userLanguage)
    {
        var builder = new StringBuilder();
        AppendField(builder, "lang", userLanguage);

        foreach (var topic in topics.OrderBy(x => x.Id))
        {
            AppendField(builder, "topic", topic.Id, topic.Name, topic.Description);
        }

        foreach (var subtopic in subtopics.OrderBy(x => x.Id))
        {
            AppendField(builder, "subtopic", subtopic.Id, subtopic.TopicId, subtopic.Name);
        }

        foreach (var question in questions.OrderBy(x => x.Id))
        {
            AppendField(
                builder,
                "question",
                question.Id,
                question.Type,
                question.Difficulty,
                TranslationHelper.GetText(question, userLanguage),
                question.TextFormat,
                TranslationHelper.GetExplanation(question, userLanguage),
                question.ExplanationFormat,
                TranslationHelper.GetHintLight(question, userLanguage),
                TranslationHelper.GetHintMedium(question, userLanguage),
                TranslationHelper.GetHintFull(question, userLanguage),
                question.HintFormat,
                question.TextRenderMode,
                question.ExplanationRenderMode,
                question.HintRenderMode,
                question.SemanticsAltText);

            foreach (var option in question.Options.OrderBy(o => o.Order).ThenBy(o => o.Id))
            {
                AppendField(
                    builder,
                    "option",
                    option.Id,
                    option.Order,
                    TranslationHelper.GetOptionText(option, userLanguage),
                    option.TextFormat,
                    option.RenderMode,
                    TranslationHelper.GetOptionSemanticsAltText(option, userLanguage),
                    option.IsCorrect);
            }

            foreach (var translation in question.Translations.OrderBy(x => x.Lang))
            {
                AppendField(
                    builder,
                    "translation",
                    translation.Lang,
                    translation.Text,
                    translation.Explanation,
                    translation.HintFormula,
                    translation.HintClue,
                    translation.HintLight,
                    translation.HintMedium,
                    translation.HintFull);
            }

            foreach (var step in question.Steps.OrderBy(x => x.StepIndex))
            {
                AppendField(
                    builder,
                    "step",
                    step.StepIndex,
                    step.Text,
                    step.Hint,
                    step.Highlight,
                    step.TextFormat,
                    step.HintFormat,
                    step.TextRenderMode,
                    step.HintRenderMode,
                    step.SemanticsAltText,
                    TranslationHelper.GetStepSemanticsAltText(step, userLanguage));

                foreach (var translation in step.Translations.OrderBy(x => x.Lang))
                {
                    AppendField(
                        builder,
                        "step-translation",
                        translation.Lang,
                        translation.Text,
                        translation.Hint);
                }
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static string ComputeSnapshotVersion(
        UserProfile? profile,
        IReadOnlyList<OfflineBundleQuestionProgressDto> questionProgress,
        IReadOnlyDictionary<int, QuestionStat> reviewStats)
    {
        var builder = new StringBuilder();
        AppendField(
            builder,
            "profile",
            profile?.Xp ?? 0,
            profile?.Level ?? 1,
            profile?.Streak ?? 0,
            profile is null ? 0 : profile.UpdatedAt.ToUniversalTime().Ticks);

        foreach (var progress in questionProgress.OrderBy(x => x.QuestionId))
        {
            AppendField(builder, "progress", progress.QuestionId, progress.Attempts, progress.CorrectAttempts, progress.LastAttemptAt?.ToUniversalTime().Ticks ?? 0);
            if (reviewStats.TryGetValue(progress.QuestionId, out var review))
            {
                AppendField(builder, "review", progress.QuestionId, review.NextReview.ToUniversalTime().Ticks);
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static string ComputeSnapshotVersion(
        UserProfile? profile,
        IReadOnlyList<UserQuestionStat> userStats,
        IReadOnlyDictionary<int, QuestionStat> reviewStats)
    {
        var progress = userStats
            .Select(x => new OfflineBundleQuestionProgressDto(
                x.QuestionId,
                x.Attempts,
                x.CorrectAttempts,
                x.LastAttemptAt,
                reviewStats.TryGetValue(x.QuestionId, out var review) ? review.NextReview : null))
            .ToList();

        return ComputeSnapshotVersion(profile, progress, reviewStats);
    }

    private static void AppendField(StringBuilder builder, params object?[] values)
    {
        foreach (var value in values)
        {
            builder.Append('|');
            AppendValue(builder, value);
        }
    }

    private static void AppendValue(StringBuilder builder, object? value)
    {
        switch (value)
        {
            case null:
                builder.Append("<null>");
                return;
            case DateTime dateTime:
                builder.Append(dateTime.ToUniversalTime().Ticks);
                return;
            case DateTimeOffset dateTimeOffset:
                builder.Append(dateTimeOffset.UtcTicks);
                return;
            case bool boolValue:
                builder.Append(boolValue ? '1' : '0');
                return;
            default:
                builder.Append(value);
                return;
        }
    }
}
