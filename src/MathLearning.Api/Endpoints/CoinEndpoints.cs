using System.Text.Json;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Endpoints;

public static class CoinEndpoints
{
    public static void MapCoinEndpoints(this IEndpointRouteBuilder app)
    {
        // Legacy compatibility routes only.
        // Do not add new backend-authoritative mobile settlement behavior under /api/coins/*.
        // Canonical authenticated mutation routes live under /api/economy/*.
        var group = app.MapGroup("/api/coins")
            .RequireAuthorization()
            .WithTags("Coins");

        group.MapGet("/balance", async (
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
                    level = 1,
                    xp = 0
                });
            }

            return Results.Ok(new
            {
                coins = profile.Coins,
                totalEarned = profile.TotalCoinsEarned,
                totalSpent = profile.TotalCoinsSpent,
                level = profile.Level,
                xp = profile.Xp,
                streak = profile.Streak
            });
        })
        .WithName("GetCoinBalance");

        group.MapPost("/earn", (HttpContext ctx) =>
        {
            ctx.Response.Headers.Append("Sunset", "2026-10-01");
            return Results.Json(
                LegacyMutationDeprecated(
                    "legacy_route_removed",
                    "Use canonical reward settlement under /api/economy/rewards/claim or audited admin grant flows."),
                statusCode: StatusCodes.Status410Gone);
        })
        .WithName("EarnCoins");

        group.MapPost("/spend", (HttpContext ctx) =>
        {
            ctx.Response.Headers.Append("Sunset", "2026-10-01");
            return Results.Json(
                LegacyMutationDeprecated(
                    "legacy_route_removed",
                    "Use canonical spend settlement under /api/economy/coins/spend."),
                statusCode: StatusCodes.Status410Gone);
        })
        .WithName("SpendCoins");

        group.MapGet("/history", async (
            ApiDbContext db,
            HttpContext ctx) =>
        {
            string userId = ctx.User.FindFirst("userId")!.Value;

            var transactions = await db.EconomyTransactions
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Status == EconomyTransactionStatus.Completed)
                .OrderByDescending(x => x.CompletedAtUtc ?? x.CreatedAtUtc)
                .Take(50)
                .ToListAsync();

            return Results.Ok(transactions.Select(BuildHistoryRow));
        })
        .WithName("GetCoinHistory");

        group.MapGet("/leaderboard", async (
            ApiDbContext db,
            int limit = 10) =>
        {
            var richestUsers = await db.UserProfiles
                .OrderByDescending(p => p.Coins)
                .Take(limit)
                .Select(p => new
                {
                    rank = 0,
                    username = p.Username,
                    coins = p.Coins,
                    level = p.Level,
                    totalEarned = p.TotalCoinsEarned,
                    totalSpent = p.TotalCoinsSpent
                })
                .ToListAsync();

            var ranked = richestUsers.Select((user, index) => new
            {
                rank = index + 1,
                user.username,
                user.coins,
                user.level,
                user.totalEarned,
                user.totalSpent
            });

            return Results.Ok(ranked);
        })
        .WithName("GetCoinLeaderboard");
    }

    private static object LegacyMutationDeprecated(string errorCode, string message) => new
    {
        success = false,
        errorCode,
        message,
        replacementRoute = "/api/economy/*",
        removalDate = "2026-10-01"
    };

    private static object BuildHistoryRow(EconomyTransaction transaction)
    {
        var amount = ResolveTransactionAmount(transaction);
        return new
        {
            type = amount >= 0 ? "earned" : "spent",
            amount,
            transactionType = transaction.TransactionType,
            idempotencyKey = transaction.IdempotencyKey,
            operationId = transaction.OperationId,
            status = transaction.Status.ToString(),
            timestamp = transaction.CompletedAtUtc ?? transaction.CreatedAtUtc
        };
    }

    private static int ResolveTransactionAmount(EconomyTransaction transaction)
    {
        return transaction.TransactionType switch
        {
            "economy_coins_spend" => -TryReadJsonInt(transaction.RequestJson, "Amount"),
            "economy_hint_use" => -FirstNonZero(
                TryReadJsonInt(transaction.ResultJson, "SpentCoins"),
                TryReadJsonInt(transaction.RequestJson, "CostCoins")),
            "shop_streak_freeze_purchase" => -TryReadJsonInt(transaction.ResultJson, "SpentCoins"),
            "admin_reward_grant" => TryReadJsonInt(transaction.RequestJson, "Coins"),
            "economy_reward_claim" => FirstNonZero(
                TryReadNestedJsonInt(transaction.ResultJson, "Reward", "Coins"),
                TryReadJsonInt(transaction.RequestJson, "Coins")),
            _ => 0
        };
    }

    private static int FirstNonZero(params int[] values)
        => values.FirstOrDefault(x => x != 0);

    private static int TryReadJsonInt(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;

        try
        {
            using var document = JsonDocument.Parse(json);
            return TryGetIntValue(document.RootElement, propertyName);
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static int TryReadNestedJsonInt(string? json, string parentProperty, string childProperty)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;

        try
        {
            using var document = JsonDocument.Parse(json);
            return TryGetPropertyValue(document.RootElement, parentProperty, out var parent)
                   && parent.ValueKind == JsonValueKind.Object
                ? TryGetIntValue(parent, childProperty)
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static int TryGetIntValue(JsonElement element, string propertyName)
        => TryGetPropertyValue(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static bool TryGetPropertyValue(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (element.TryGetProperty(propertyName, out value))
            return true;

        var camelCaseName = JsonNamingPolicy.CamelCase.ConvertName(propertyName);
        if (!string.Equals(camelCaseName, propertyName, StringComparison.Ordinal) &&
            element.TryGetProperty(camelCaseName, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
