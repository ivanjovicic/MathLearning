using MathLearning.Application.DTOs.Quiz;
using MathLearning.Application.Helpers;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class HintEndpoints
{
    private const int FormulaCost = 5;
    private const int ClueCost = 10;
    private const int EliminateCost = 15;
    private const int SolutionCost = 20;

    public static void MapHintEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hints")
            .RequireAuthorization()
            .WithTags("Hints");
        var legacyGroup = app.MapGroup("/api/questions")
            .RequireAuthorization()
            .WithTags("Hints");

        legacyGroup.MapGet("/{id:int}/hint/formula", (HttpContext ctx) =>
        {
            ctx.Response.Headers.Append("Sunset", "2026-10-01");
            return Results.Json(DeprecatedHintAlias("/api/hints/questions/{id}/formula"), statusCode: StatusCodes.Status410Gone);
        });

        legacyGroup.MapGet("/{id:int}/hint/clue", (HttpContext ctx) =>
        {
            ctx.Response.Headers.Append("Sunset", "2026-10-01");
            return Results.Json(DeprecatedHintAlias("/api/hints/questions/{id}/clue"), statusCode: StatusCodes.Status410Gone);
        });

        legacyGroup.MapPost("/{id:int}/hint/eliminate", (HttpContext ctx) =>
        {
            ctx.Response.Headers.Append("Sunset", "2026-10-01");
            return Results.Json(DeprecatedHintAlias("/api/hints/questions/{id}/eliminate"), statusCode: StatusCodes.Status410Gone);
        });

        group.MapGet("/questions/{id}/formula", async (
            int id,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            var userId = ctx.User.FindFirst("userId")!.Value;
            var lang = await ResolveUserLang(db, ctx, userId);

            var question = await db.Questions
                .Include(q => q.Translations)
                .FirstOrDefaultAsync(q => q.Id == id);
            if (question == null)
                return Results.NotFound(new { error = "Question not found" });

            var formula = TranslationHelper.GetHintFormula(question, lang);
            if (string.IsNullOrEmpty(formula))
                return Results.Ok(new { formula = (string?)null, available = false });

            var unlocked = await HasUnlockedHintAsync(db, userId, id, "formula");
            if (!unlocked)
            {
                return Results.Ok(new
                {
                    formula = (string?)null,
                    available = true,
                    alreadyUsed = false,
                    requiresUnlock = true,
                    cost = FormulaCost
                });
            }

            return Results.Ok(new
            {
                formula,
                available = true,
                alreadyUsed = true,
                cost = 0
            });
        })
        .WithName("GetFormulaHint");

        group.MapGet("/questions/{id}/clue", async (
            int id,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            var userId = ctx.User.FindFirst("userId")!.Value;
            var lang = await ResolveUserLang(db, ctx, userId);

            var question = await db.Questions
                .Include(q => q.Translations)
                .FirstOrDefaultAsync(q => q.Id == id);
            if (question == null)
                return Results.NotFound(new { error = "Question not found" });

            var clue = TranslationHelper.GetHintClue(question, lang);
            if (string.IsNullOrEmpty(clue))
                return Results.Ok(new { clue = (string?)null, available = false });

            var unlocked = await HasUnlockedHintAsync(db, userId, id, "clue");
            if (!unlocked)
            {
                return Results.Ok(new
                {
                    clue = (string?)null,
                    available = true,
                    alreadyUsed = false,
                    requiresUnlock = true,
                    cost = ClueCost
                });
            }

            return Results.Ok(new
            {
                clue,
                available = true,
                alreadyUsed = true,
                cost = 0
            });
        })
        .WithName("GetClueHint");

        group.MapPost("/questions/{id}/eliminate", async (
            int id,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            var userId = ctx.User.FindFirst("userId")!.Value;
            var lang = await ResolveUserLang(db, ctx, userId);

            var question = await db.Questions
                .Include(q => q.Options).ThenInclude(o => o.Translations)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
                return Results.NotFound(new { error = "Question not found" });

            if (question.Type != "multiple_choice")
                return Results.BadRequest(new { error = "Eliminate only works for multiple choice questions" });

            var unlocked = await HasUnlockedHintAsync(db, userId, id, "eliminate");
            if (!unlocked)
            {
                return Results.Conflict(new
                {
                    success = false,
                    errorCode = "hint_not_unlocked",
                    message = "Unlock this hint first through /api/economy/hints/use.",
                    cost = EliminateCost
                });
            }

            var payload = BuildEliminatePayload(question, lang, userId);
            return Results.Ok(new
            {
                payload.remainingOptions,
                payload.remainingOptionIds,
                payload.eliminatedOption,
                cost = 0,
                alreadyUsed = true
            });
        })
        .WithName("EliminateWrongOption");

        group.MapGet("/questions/{id}/solution", async (
            int id,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            var userId = ctx.User.FindFirst("userId")!.Value;
            var lang = await ResolveUserLang(db, ctx, userId);

            var question = await db.Questions
                .Include(q => q.Translations)
                .FirstOrDefaultAsync(q => q.Id == id);
            if (question == null)
                return Results.NotFound(new { error = "Question not found" });

            var explanation = TranslationHelper.GetExplanation(question, lang);
            if (string.IsNullOrEmpty(explanation))
                return Results.Ok(new { solution = (string?)null, available = false });

            var unlocked = await HasUnlockedHintAsync(db, userId, id, "solution");
            if (!unlocked)
            {
                return Results.Ok(new
                {
                    solution = (string?)null,
                    available = true,
                    alreadyUsed = false,
                    requiresUnlock = true,
                    cost = SolutionCost
                });
            }

            return Results.Ok(new
            {
                solution = explanation,
                available = true,
                alreadyUsed = true,
                cost = 0
            });
        })
        .WithName("GetSolutionHint");

        group.MapGet("/coins", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                return Results.Ok(new
                {
                    coins = 0,
                    totalEarned = 0,
                    totalSpent = 0,
                    message = "Profile not found"
                });
            }

            return Results.Ok(new
            {
                coins = profile.Coins,
                totalEarned = profile.TotalCoinsEarned,
                totalSpent = profile.TotalCoinsSpent
            });
        })
        .WithName("GetUserCoins");

        group.MapGet("/stats", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var hints = await db.UserHints
                .Where(h => h.UserId == userId)
                .ToListAsync();

            if (!hints.Any())
            {
                return Results.Ok(new HintStatsDto(
                    TotalHintsUsed: 0,
                    FormulaHintsUsed: 0,
                    ClueHintsUsed: 0,
                    SolutionHintsUsed: 0,
                    AverageHintsPerQuestion: 0
                ));
            }

            var totalHints = hints.Count;
            var formulaHints = hints.Count(h => h.HintType == "formula");
            var clueHints = hints.Count(h => h.HintType == "clue");
            var solutionHints = hints.Count(h => h.HintType == "solution");

            var questionsWithHints = hints.Select(h => h.QuestionId).Distinct().Count();
            var avgHintsPerQuestion = questionsWithHints > 0
                ? Math.Round((double)totalHints / questionsWithHints, 2)
                : 0;

            return Results.Ok(new HintStatsDto(
                TotalHintsUsed: totalHints,
                FormulaHintsUsed: formulaHints,
                ClueHintsUsed: clueHints,
                SolutionHintsUsed: solutionHints,
                AverageHintsPerQuestion: avgHintsPerQuestion
            ));
        })
        .WithName("GetHintStats")
        .WithDescription("Get user's hint usage statistics");

        group.MapGet("/history", async (
            ApiDbContext db,
            HttpContext ctx,
            int limit = 50) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var hints = await db.UserHints
                .Include(h => h.Question)
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.UsedAt)
                .Take(limit)
                .Select(h => new
                {
                    h.Id,
                    h.QuestionId,
                    QuestionText = h.Question!.Text,
                    h.HintType,
                    h.UsedAt
                })
                .ToListAsync();

            return Results.Ok(hints);
        })
        .WithName("GetHintHistory")
        .WithDescription("Get user's hint usage history");

        group.MapGet("/question/{questionId}", async (
            int questionId,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var question = await db.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                return Results.NotFound(new { error = "Question not found" });

            var usedHints = await db.UserHints
                .Where(h => h.UserId == userId && h.QuestionId == questionId)
                .Select(h => h.HintType)
                .ToListAsync();

            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            var currentCoins = profile?.Coins ?? 0;

            return Results.Ok(new
            {
                questionId,
                currentCoins,
                availableHints = new
                {
                    formula = new
                    {
                        available = !string.IsNullOrEmpty(question.HintFormula),
                        used = usedHints.Contains("formula"),
                        cost = FormulaCost,
                        affordable = currentCoins >= FormulaCost
                    },
                    clue = new
                    {
                        available = !string.IsNullOrEmpty(question.HintClue),
                        used = usedHints.Contains("clue"),
                        cost = ClueCost,
                        affordable = currentCoins >= ClueCost
                    },
                    eliminate = new
                    {
                        available = question.Type == "multiple_choice" && question.Options.Count > 2,
                        used = usedHints.Contains("eliminate"),
                        cost = EliminateCost,
                        affordable = currentCoins >= EliminateCost
                    },
                    solution = new
                    {
                        available = !string.IsNullOrEmpty(question.Explanation),
                        used = usedHints.Contains("solution"),
                        cost = SolutionCost,
                        affordable = currentCoins >= SolutionCost
                    }
                }
            });
        })
        .WithName("GetQuestionHintsSummary");
    }

    private static object DeprecatedHintAlias(string replacementRoute) => new
    {
        success = false,
        errorCode = "legacy_route_removed",
        message = $"Use canonical hint routes and settlement under {replacementRoute} plus /api/economy/hints/use.",
        replacementRoute,
        removalDate = "2026-10-01"
    };

    private static async Task<bool> HasUnlockedHintAsync(ApiDbContext db, string userId, int questionId, string hintType)
        => await db.UserHints.AnyAsync(h => h.UserId == userId && h.QuestionId == questionId && h.HintType == hintType);

    private static (IReadOnlyList<string> remainingOptions, IReadOnlyList<string> remainingOptionIds, string eliminatedOption) BuildEliminatePayload(
        Question question,
        string lang,
        string userId)
    {
        var wrongOptions = question.Options
            .Where(o => !o.IsCorrect)
            .OrderBy(o => o.Order)
            .ThenBy(o => o.Id)
            .ToList();
        if (wrongOptions.Count == 0)
            throw new InvalidOperationException("No wrong options to eliminate");

        var seed = $"{userId}:{question.Id}:eliminate";
        var index = Math.Abs(seed.GetHashCode()) % wrongOptions.Count;
        var toEliminate = wrongOptions[index];

        var remaining = question.Options
            .Where(o => o.Id != toEliminate.Id)
            .OrderBy(o => o.Order)
            .ThenBy(o => o.Id)
            .ToList();

        return (
            remaining.Select(o => TranslationHelper.GetOptionText(o, lang)).ToList(),
            remaining.Select(o => o.Id.ToString()).ToList(),
            TranslationHelper.GetOptionText(toEliminate, lang));
    }

    private static async Task<string> ResolveUserLang(ApiDbContext db, HttpContext ctx, string userId)
    {
        var settings = await db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        var acceptLang = ctx.Request.Headers.AcceptLanguage.FirstOrDefault();
        return TranslationHelper.ResolveLanguage(settings?.Language, acceptLang);
    }
}
