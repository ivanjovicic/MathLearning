using MathLearning.Application.DTOs.Quiz;
using MathLearning.Application.Helpers;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class QuestionEndpoints
{
    public static void MapQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/questions")
                       .RequireAuthorization()
                       .WithTags("Questions");

        // 📚 GET QUESTIONS with language support
        group.MapGet("/", async (
            ApiDbContext db,
            HttpContext ctx,
            [FromQuery] string? lang = null,
            [FromQuery] int? subtopicId = null,
            [FromQuery] int limit = 20) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            string resolvedLang = await ResolveUserLang(db, ctx, userId, lang);

            var query = db.Questions
                .Include(q => q.Options).ThenInclude(o => o.Translations)
                .Include(q => q.Translations)
                .Include(q => q.Steps).ThenInclude(s => s.Translations)
                .AsQueryable();

            if (subtopicId.HasValue)
            {
                query = query.Where(q => q.SubtopicId == subtopicId.Value);
            }

            var questions = await query
                .OrderBy(q => q.Difficulty)
                .Take(limit)
                .ToListAsync();

            var questionDtos = questions.Select(q => MapPreAnswerQuestion(q, resolvedLang)).ToList();

            return Results.Ok(questionDtos);
        })
        .WithName("GetQuestions")
        .WithDescription("Get questions with language support and fallback");

        // 🔍 GET SINGLE QUESTION
        group.MapGet("/{id}", async (
            int id,
            ApiDbContext db,
            HttpContext ctx,
            [FromQuery] string? lang = null) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;
            string resolvedLang = await ResolveUserLang(db, ctx, userId, lang);

            var question = await db.Questions
                .Include(q => q.Options).ThenInclude(o => o.Translations)
                .Include(q => q.Translations)
                .Include(q => q.Steps).ThenInclude(s => s.Translations)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
                return Results.NotFound(new { error = "Question not found" });

            var dto = MapPreAnswerQuestion(question, resolvedLang);

            return Results.Ok(dto);
        })
        .WithName("GetQuestion")
        .WithDescription("Get single question with language support");
    }

    private static async Task<string> ResolveUserLang(ApiDbContext db, HttpContext ctx, string userId, string? explicitLang = null)
    {
        // 1. Explicit lang parameter takes precedence
        if (!string.IsNullOrWhiteSpace(explicitLang))
            return explicitLang.Trim().ToLowerInvariant();

        // 2. User settings
        var settings = await db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        // 3. Accept-Language header
        var acceptLang = ctx.Request.Headers.AcceptLanguage.FirstOrDefault();

        return TranslationHelper.ResolveLanguage(settings?.Language, acceptLang);
    }

    private static OptionDto MapOptionDto(QuestionOption option, string language)
        => new(
            option.Id,
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetOptionText(option, language)) ?? string.Empty,
            option.TextFormat,
            option.RenderMode,
            TranslationHelper.GetOptionSemanticsAltText(option, language));

    private static QuizQuestionDto MapPreAnswerQuestion(Question question, string language)
        => new(
            question.Id,
            question.Type,
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetText(question, language)) ?? string.Empty,
            question.Options.Select(option => MapOptionDto(option, language)).ToList(),
            question.Difficulty,
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintLight(question, language)),
            InlineLatexFormatter.NormalizeMixedInlineMath(TranslationHelper.GetHintMedium(question, language)),
            question.TextFormat,
            question.HintFormat,
            question.TextRenderMode,
            question.HintRenderMode,
            TranslationHelper.GetQuestionSemanticsAltText(question, language));
}
