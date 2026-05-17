using MathLearning.Application.DTOs.Progress;
using MathLearning.Application.DTOs.Quiz;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class SrsEndpoints
{
    public static void MapSrsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quiz")
                       .RequireAuthorization()
                       .WithTags("Quiz");

        // ?? SRS UPDATE
        group.MapPost("/srs/update", async (
            SrsUpdateDto dto,
            ISrsService srs,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var result = await srs.UpdateAsync(userId, dto);

            return Results.Ok(new
            {
                questionId = result.QuestionId,
                nextReview = result.NextReview,
                streak = result.SuccessStreak,
                ease = result.Ease
            });
        });

        // ?? SRS DAILY
        group.MapGet("/srs/daily", async (
            ApiDbContext db,
            HttpContext ctx,
            MathLearning.Api.Services.LegacyStepExplanationAdapter stepAdapter,
            int limit = 20) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            string lang = await ResolveUserLang(db, ctx, userId);

            var dueQuestionIds = await db.QuestionStats
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.NextReview <= DateTime.UtcNow)
                .OrderBy(x => x.Ease)
                .ThenBy(x => x.QuestionId)
                .Take(limit)
                .Select(x => x.QuestionId)
                .ToListAsync();
            var questions = await LoadQuestionsWithDetailsByIds(db, dueQuestionIds);

            // Legacy/mobile UX fallback:
            // if user has at least one due SRS item, pad the session with random questions
            // so quiz flow doesn't stop after a single card.
            int targetCount = Math.Min(limit, 10);
            if (questions.Count > 0 && questions.Count < targetCount)
            {
                var dueIds = questions.Select(q => q.Id).ToList();
                int needed = targetCount - questions.Count;

                var randomFillIds = await SelectRandomQuestionIdsAsync(
                    db.Questions.AsNoTracking().Where(q => !dueIds.Contains(q.Id)),
                    needed,
                    ctx.RequestAborted);
                var randomFill = await LoadQuestionsWithDetailsByIds(db, randomFillIds);

                questions.AddRange(randomFill);
            }

            return Results.Ok(questions.Select(q => MapQuestionDto(q, lang, stepAdapter)).ToList());
        });

        // ?? SRS MIXED (due + random)
        group.MapGet("/srs/mixed", async (
            ApiDbContext db,
            HttpContext ctx,
            MathLearning.Api.Services.LegacyStepExplanationAdapter stepAdapter,
            int count = 15) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            string lang = await ResolveUserLang(db, ctx, userId);

            var dueStats = await db.QuestionStats
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.NextReview <= DateTime.UtcNow)
                .OrderBy(x => x.Ease)
                .Take(count)
                .ToListAsync();

            var dueIds = dueStats.Select(x => x.QuestionId).ToList();

            var srsQuestions = await LoadQuestionsWithDetailsByIds(db, dueIds);

            int needed = count - srsQuestions.Count;

            List<Question> randomQuestions = new();

            if (needed > 0)
            {
                var randomQuestionIds = await SelectRandomQuestionIdsAsync(
                    db.Questions.AsNoTracking().Where(x => !dueIds.Contains(x.Id)),
                    needed,
                    ctx.RequestAborted);
                randomQuestions = await LoadQuestionsWithDetailsByIds(db, randomQuestionIds);
            }

            return Results.Ok(new
            {
                srs = srsQuestions.Select(q => MapQuestionDto(q, lang, stepAdapter)),
                random = randomQuestions.Select(q => MapQuestionDto(q, lang, stepAdapter))
            });
        });

        group.MapGet("/srs/streak", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            StreakRollEventDto? streakEvent = null;

            if (profile != null)
            {
                var roll = StreakRoller.Apply(profile, today);
                if (roll != null)
                {
                    streakEvent = new StreakRollEventDto(
                        roll.Type,
                        roll.MissedDays,
                        roll.FreezesUsed,
                        roll.StreakBefore,
                        roll.StreakAfter
                    );
                    await db.SaveChangesAsync();
                }
            }

            return Results.Ok(new
            {
                streak = profile?.Streak ?? 0,
                streakFreezeCount = profile?.StreakFreezeCount ?? 0,
                lastStreakDay = profile?.LastStreakDay,
                lastActivityDay = profile?.LastActivityDay,
                streakEvent = streakEvent
            });
        });
    }

    // ??? Shared helper to map Question entity ? QuestionDto with translation + steps
    private static QuestionDto MapQuestionDto(Question q, string lang, MathLearning.Api.Services.LegacyStepExplanationAdapter stepAdapter)
    {
        var options = q.Options
            .Select(o => new OptionDto(
                o.Id,
                InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetOptionText(o, lang)) ?? string.Empty,
                o.TextFormat,
                o.RenderMode,
                TranslationHelper.GetOptionSemanticsAltText(o, lang)))
            .ToList();

        return new QuestionDto(
            q.Id,
            q.Type,
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetText(q, lang)) ?? string.Empty,
            options,
            q.Options.FirstOrDefault(o => o.IsCorrect)?.Id ?? 0,
            q.Difficulty,
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintLight(q, lang)),
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintMedium(q, lang)),
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintFull(q, lang)),
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetExplanation(q, lang)),
            NormalizeStepsForResponse(stepAdapter.GetSteps(q, lang)),
            q.TextFormat,
            q.ExplanationFormat,
            q.HintFormat,
            q.TextRenderMode,
            q.ExplanationRenderMode,
            q.HintRenderMode,
            TranslationHelper.GetQuestionSemanticsAltText(q, lang)
        );
    }

    private static List<StepExplanationDto> NormalizeStepsForResponse(List<StepExplanationDto> steps)
    {
        return steps
            .Select(step => new StepExplanationDto(
                InlineLatexFormatter.NormalizeMixedInlineMath(step.Text) ?? step.Text,
                InlineLatexFormatter.NormalizeMixedInlineMath(step.Hint),
                step.Highlight,
                step.TextFormat,
                step.HintFormat,
                step.TextRenderMode,
                step.HintRenderMode,
                TranslationHelper.ResolveSemanticsAltText(step.SemanticsAltText, step.Text, step.TextFormat)))
            .ToList();
    }

    // Load full question graph in a deterministic order by pre-selected IDs.
    // This avoids empty collection navigations when random ordering + Take is used.
    private static async Task<List<Question>> LoadQuestionsWithDetailsByIds(ApiDbContext db, IReadOnlyList<int> orderedQuestionIds)
    {
        if (orderedQuestionIds.Count == 0)
            return new List<Question>();

        var questions = await db.Questions
            .Where(q => orderedQuestionIds.Contains(q.Id))
            .Include(q => q.Options).ThenInclude(o => o.Translations)
            .Include(q => q.Translations)
            .Include(q => q.Steps).ThenInclude(s => s.Translations)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync();

        var orderMap = orderedQuestionIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);

        return questions
            .OrderBy(q => orderMap.TryGetValue(q.Id, out var index) ? index : int.MaxValue)
            .ToList();
    }

    private static async Task<List<int>> SelectRandomQuestionIdsAsync(
        IQueryable<Question> baseQuery,
        int count,
        CancellationToken ct)
    {
        if (count <= 0)
        {
            return [];
        }

        var total = await baseQuery.CountAsync(ct);
        if (total <= count)
        {
            return await baseQuery
                .OrderBy(q => q.Id)
                .Select(q => q.Id)
                .ToListAsync(ct);
        }

        var orderedIdsQuery = baseQuery
            .OrderBy(q => q.Id)
            .Select(q => q.Id);

        var skip = Random.Shared.Next(0, total);
        var ids = await orderedIdsQuery
            .Skip(skip)
            .Take(count)
            .ToListAsync(ct);

        if (ids.Count < count)
        {
            var remaining = count - ids.Count;
            var wrapAroundIds = await orderedIdsQuery
                .Take(remaining)
                .ToListAsync(ct);

            ids.AddRange(wrapAroundIds);
        }

        return ids
            .Distinct()
            .Take(count)
            .ToList();
    }

    // ??? Shared helper for resolving user language
    private static async Task<string> ResolveUserLang(ApiDbContext db, HttpContext ctx, string userId)
    {
        var cacheKey = $"req:user-settings:{userId}";
        UserSettings? settings = null;
        if (ctx.Items.TryGetValue(cacheKey, out var cached) && cached is UserSettings cachedSettings)
        {
            settings = cachedSettings;
        }
        else
        {
            settings = await db.UserSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);
            if (settings is not null)
            {
                ctx.Items[cacheKey] = settings;
            }
        }

        var acceptLang = ctx.Request.Headers.AcceptLanguage.FirstOrDefault();
        return TranslationHelper.ResolveLanguage(settings?.Language, acceptLang);
    }
}
