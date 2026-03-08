using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.Services;

namespace MathLearning.Api.Endpoints;

public static class AvatarEndpoints
{
    public static void MapAvatarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cosmetics")
            .RequireAuthorization()
            .WithTags("Cosmetics & Avatar");

        group.MapGet("/items", async (
            ICosmeticCatalogService catalogService,
            HttpContext ctx,
            CancellationToken cancellationToken,
            string? category,
            string? rarity,
            int? seasonId) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await catalogService.GetCatalogAsync(userId, category, rarity, seasonId, cancellationToken));
        });

        group.MapGet("/inventory", async (
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken,
            string? category) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await inventoryService.GetInventoryAsync(userId, category, cancellationToken));
        });

        group.MapGet("/avatar", async (
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await inventoryService.GetAvatarAsync(userId, cancellationToken));
        });

        group.MapPut("/avatar", async (
            UpdateAvatarConfigRequest request,
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await inventoryService.UpdateAvatarAsync(userId, request, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/equip", async (
            EquipCosmeticRequest request,
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await inventoryService.EquipSlotAsync(userId, request, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/equip-batch", async (
            EquipCosmeticBatchRequest request,
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await inventoryService.EquipBatchAsync(userId, request, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/unequip", async (
            EquipCosmeticRequest request,
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await inventoryService.EquipSlotAsync(userId, request with { CosmeticItemId = null }, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/purchase", async (
            PurchaseCosmeticRequest request,
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await inventoryService.PurchaseAsync(userId, request, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/seasons", async (
            ICosmeticCatalogService catalogService,
            bool activeOnly,
            CancellationToken cancellationToken) =>
            Results.Ok(await catalogService.GetSeasonsAsync(activeOnly, cancellationToken)))
            .AllowAnonymous();

        group.MapGet("/reward-track", async (
            ICosmeticCatalogService catalogService,
            HttpContext ctx,
            CancellationToken cancellationToken,
            int? seasonId,
            string? trackType) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var response = await catalogService.GetRewardTrackAsync(userId, seasonId, trackType ?? "free", cancellationToken);
            return response is null ? Results.NotFound(new { error = "Reward track not found." }) : Results.Ok(response);
        });

        group.MapGet("/avatar/{userId}", async (
            string userId,
            ICosmeticInventoryService inventoryService,
            CancellationToken cancellationToken) =>
            Results.Ok(await inventoryService.GetPublicAppearanceAsync(userId, cancellationToken)))
            .AllowAnonymous();

        app.MapGet("/api/avatar/me", async (
            ICosmeticInventoryService inventoryService,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            var userId = ctx.User.FindFirst("userId")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await inventoryService.GetAvatarAsync(userId, cancellationToken));
        }).RequireAuthorization();

        app.MapGet("/api/profile/{userId}/appearance", async (
            string userId,
            ICosmeticInventoryService inventoryService,
            CancellationToken cancellationToken) =>
            Results.Ok(await inventoryService.GetPublicAppearanceAsync(userId, cancellationToken)))
            .AllowAnonymous();
    }
}
