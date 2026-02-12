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
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);
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

            var questionDtos = questions.Select(q => new QuestionDto(
                q.Id,
                q.Type,
                TranslationHelper.GetText(q, resolvedLang),
                q.Options.Select(o => new OptionDto(o.Id, TranslationHelper.GetOptionText(o, resolvedLang))).ToList(),
                q.Options.FirstOrDefault(o => o.IsCorrect)?.Id ?? 0,
                q.Difficulty,
                TranslationHelper.GetHintLight(q, resolvedLang),
                TranslationHelper.GetHintMedium(q, resolvedLang),
                TranslationHelper.GetHintFull(q, resolvedLang),
                TranslationHelper.GetExplanation(q, resolvedLang),
                StepEngine.GetSteps(q, resolvedLang)
            )).ToList();

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
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);
            string resolvedLang = await ResolveUserLang(db, ctx, userId, lang);

            var question = await db.Questions
                .Include(q => q.Options).ThenInclude(o => o.Translations)
                .Include(q => q.Translations)
                .Include(q => q.Steps).ThenInclude(s => s.Translations)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
                return Results.NotFound(new { error = "Question not found" });

            var dto = new QuestionDto(
                question.Id,
                question.Type,
                TranslationHelper.GetText(question, resolvedLang),
                question.Options.Select(o => new OptionDto(o.Id, TranslationHelper.GetOptionText(o, resolvedLang))).ToList(),
                question.Options.FirstOrDefault(o => o.IsCorrect)?.Id ?? 0,
                question.Difficulty,
                TranslationHelper.GetHintLight(question, resolvedLang),
                TranslationHelper.GetHintMedium(question, resolvedLang),
                TranslationHelper.GetHintFull(question, resolvedLang),
                TranslationHelper.GetExplanation(question, resolvedLang),
                StepEngine.GetSteps(question, resolvedLang)
            );

            return Results.Ok(dto);
        })
        .WithName("GetQuestion")
        .WithDescription("Get single question with language support");
    }

    private static async Task<string> ResolveUserLang(ApiDbContext db, HttpContext ctx, int userId, string? explicitLang = null)
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
}
