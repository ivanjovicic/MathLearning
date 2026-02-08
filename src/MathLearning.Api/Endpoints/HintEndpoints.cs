using MathLearning.Application.DTOs.Quiz;
using MathLearning.Application.Helpers;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class HintEndpoints
{
    // 💰 Hint costs
    private const int FORMULA_COST = 5;
    private const int CLUE_COST = 10;
    private const int ELIMINATE_COST = 15;
    private const int SOLUTION_COST = 20;

    public static void MapHintEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hints")
                       .RequireAuthorization()
                       .WithTags("Hints");

        // 💡 GET FORMULA HINT
        group.MapGet("/questions/{id}/formula", async (
            int id,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);
            string lang = await ResolveUserLang(db, ctx, userId);

            var question = await db.Questions
                .Include(q => q.Translations)
                .FirstOrDefaultAsync(q => q.Id == id);
            if (question == null)
                return Results.NotFound(new { error = "Question not found" });

            var formula = TranslationHelper.GetHintFormula(question, lang);
            if (string.IsNullOrEmpty(formula))
                return Results.Ok(new { formula = (string?)null, available = false });

            // Check if already used
            var alreadyUsed = await db.UserHints
                .AnyAsync(h => h.UserId == userId && h.QuestionId == id && h.HintType == "formula");

            if (alreadyUsed)
            {
                // Free if already used
                return Results.Ok(new
                {
                    formula = formula,
                    available = true,
                    alreadyUsed = true,
                    cost = 0
                });
            }

            // Check coins
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null || profile.Coins < FORMULA_COST)
            {
                return Results.Json(new
                {
                    error = "Insufficient coins",
                    required = FORMULA_COST,
                    current = profile?.Coins ?? 0
                }, statusCode: 402); // Payment Required
            }

            // Deduct coins
            profile.Coins -= FORMULA_COST;
            profile.TotalCoinsSpent += FORMULA_COST;
            profile.UpdatedAt = DateTime.UtcNow;

            // Record usage
            db.UserHints.Add(new UserHint
            {
                UserId = userId,
                QuestionId = id,
                HintType = "formula",
                UsedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                formula = formula,
                available = true,
                cost = FORMULA_COST,
                remainingCoins = profile.Coins
            });
        })
        .WithName("GetFormulaHint");

        // 🔍 GET CLUE HINT
        group.MapGet("/questions/{id}/clue", async (
            int id,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);
            string lang = await ResolveUserLang(db, ctx, userId);

            var question = await db.Questions
                .Include(q => q.Translations)
                .FirstOrDefaultAsync(q => q.Id == id);
            if (question == null)
                return Results.NotFound(new { error = "Question not found" });

            var clue = TranslationHelper.GetHintClue(question, lang);
            if (string.IsNullOrEmpty(clue))
                return Results.Ok(new { clue = (string?)null, available = false });

            // Check if already used
            var alreadyUsed = await db.UserHints
                .AnyAsync(h => h.UserId == userId && h.QuestionId == id && h.HintType == "clue");

            if (alreadyUsed)
            {
                return Results.Ok(new
                {
                    clue,
                    available = true,
                    alreadyUsed = true,
                    cost = 0
                });
            }

            // Check coins
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null || profile.Coins < CLUE_COST)
            {
                return Results.Json(new
                {
                    error = "Insufficient coins",
                    required = CLUE_COST,
                    current = profile?.Coins ?? 0
                }, statusCode: 402);
            }

            // Deduct coins
            profile.Coins -= CLUE_COST;
            profile.TotalCoinsSpent += CLUE_COST;
            profile.UpdatedAt = DateTime.UtcNow;

            // Record usage
            db.UserHints.Add(new UserHint
            {
                UserId = userId,
                QuestionId = id,
                HintType = "clue",
                UsedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                clue,
                available = true,
                cost = CLUE_COST,
                remainingCoins = profile.Coins
            });
        })
        .WithName("GetClueHint");

        // ❌ ELIMINATE WRONG OPTION
        group.MapPost("/questions/{id}/eliminate", async (
            int id,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);
            string lang = await ResolveUserLang(db, ctx, userId);

            var question = await db.Questions
                .Include(q => q.Options).ThenInclude(o => o.Translations)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
                return Results.NotFound(new { error = "Question not found" });

            if (question.Type != "multiple_choice")
                return Results.BadRequest(new { error = "Eliminate only works for multiple choice questions" });

            // Check if already used
            var alreadyUsed = await db.UserHints
                .AnyAsync(h => h.UserId == userId && h.QuestionId == id && h.HintType == "eliminate");

            if (alreadyUsed)
            {
                return Results.Json(new
                {
                    error = "Eliminate hint already used for this question"
                }, statusCode: 409); // Conflict
            }

            // Check coins
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null || profile.Coins < ELIMINATE_COST)
            {
                return Results.Json(new
                {
                    error = "Insufficient coins",
                    required = ELIMINATE_COST,
                    current = profile?.Coins ?? 0
                }, statusCode: 402);
            }

            // Eliminate one wrong option
            var wrongOptions = question.Options.Where(o => !o.IsCorrect).ToList();

            if (wrongOptions.Count == 0)
                return Results.BadRequest(new { error = "No wrong options to eliminate" });

            // Random eliminate one wrong option
            var toEliminate = wrongOptions.OrderBy(_ => Guid.NewGuid()).First();
            var remainingOptions = question.Options
                .Where(o => o.Id != toEliminate.Id)
                .Select(o => TranslationHelper.GetOptionText(o, lang))
                .ToList();

            // Deduct coins
            profile.Coins -= ELIMINATE_COST;
            profile.TotalCoinsSpent += ELIMINATE_COST;
            profile.UpdatedAt = DateTime.UtcNow;

            // Record usage
            db.UserHints.Add(new UserHint
            {
                UserId = userId,
                QuestionId = id,
                HintType = "eliminate",
                UsedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                remainingOptions,
                eliminatedOption = TranslationHelper.GetOptionText(toEliminate, lang),
                cost = ELIMINATE_COST,
                remainingCoins = profile.Coins
            });
        })
        .WithName("EliminateWrongOption");

        // 📖 GET SOLUTION (most expensive)
        group.MapGet("/questions/{id}/solution", async (
            int id,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);
            string lang = await ResolveUserLang(db, ctx, userId);

            var question = await db.Questions
                .Include(q => q.Translations)
                .FirstOrDefaultAsync(q => q.Id == id);
            if (question == null)
                return Results.NotFound(new { error = "Question not found" });

            var explanation = TranslationHelper.GetExplanation(question, lang);
            if (string.IsNullOrEmpty(explanation))
                return Results.Ok(new { solution = (string?)null, available = false });

            // Check if already used
            var alreadyUsed = await db.UserHints
                .AnyAsync(h => h.UserId == userId && h.QuestionId == id && h.HintType == "solution");

            if (alreadyUsed)
            {
                return Results.Ok(new
                {
                    solution = explanation,
                    available = true,
                    alreadyUsed = true,
                    cost = 0
                });
            }

            // Check coins
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null || profile.Coins < SOLUTION_COST)
            {
                return Results.Json(new
                {
                    error = "Insufficient coins",
                    required = SOLUTION_COST,
                    current = profile?.Coins ?? 0
                }, statusCode: 402);
            }

            // Deduct coins
            profile.Coins -= SOLUTION_COST;
            profile.TotalCoinsSpent += SOLUTION_COST;
            profile.UpdatedAt = DateTime.UtcNow;

            // Record usage
            db.UserHints.Add(new UserHint
            {
                UserId = userId,
                QuestionId = id,
                HintType = "solution",
                UsedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                solution = explanation,
                available = true,
                cost = SOLUTION_COST,
                remainingCoins = profile.Coins
            });
        })
        .WithName("GetSolutionHint");

        // 💰 GET USER COINS
        group.MapGet("/coins", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

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

        // 📊 GET HINT STATS
        group.MapGet("/stats", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

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

        // 📜 GET HINT HISTORY
        group.MapGet("/history", async (
            ApiDbContext db,
            HttpContext ctx,
            int limit = 50) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

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

        // 🎯 GET QUESTION HINTS SUMMARY
        group.MapGet("/question/{questionId}", async (
            int questionId,
            ApiDbContext db,
            HttpContext ctx) =>
        {
            int userId = int.Parse(ctx.User.FindFirst("userId")!.Value);

            var question = await db.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                return Results.NotFound(new { error = "Question not found" });

            // Check which hints user already used
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
                        cost = FORMULA_COST,
                        affordable = currentCoins >= FORMULA_COST
                    },
                    clue = new
                    {
                        available = !string.IsNullOrEmpty(question.HintClue),
                        used = usedHints.Contains("clue"),
                        cost = CLUE_COST,
                        affordable = currentCoins >= CLUE_COST
                    },
                    eliminate = new
                    {
                        available = question.Type == "multiple_choice" && question.Options.Count > 2,
                        used = usedHints.Contains("eliminate"),
                        cost = ELIMINATE_COST,
                        affordable = currentCoins >= ELIMINATE_COST
                    },
                    solution = new
                    {
                        available = !string.IsNullOrEmpty(question.Explanation),
                        used = usedHints.Contains("solution"),
                        cost = SOLUTION_COST,
                        affordable = currentCoins >= SOLUTION_COST
                    }
                }
            });
        })
        .WithName("GetQuestionHintsSummary");
    }

    private static async Task<string> ResolveUserLang(ApiDbContext db, HttpContext ctx, int userId)
    {
        var settings = await db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        var acceptLang = ctx.Request.Headers.AcceptLanguage.FirstOrDefault();
        return TranslationHelper.ResolveLanguage(settings?.Language, acceptLang);
    }
}
