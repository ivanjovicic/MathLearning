using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Helpers;

internal static class CosmeticEntitlementTestSeeder
{
    public static async Task<CosmeticEntitlementDto> SeedItemEntitlementAsync(
        IServiceProvider services,
        string userId,
        int cosmeticItemId,
        string sourceType,
        string sourceRef)
    {
        using var scope = services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICosmeticEntitlementService>();
        return await service.CreateItemEntitlementAsync(
            userId,
            cosmeticItemId,
            sourceType,
            sourceRef,
            $"test:item:{userId}:{cosmeticItemId}:{sourceRef}",
            CancellationToken.None);
    }

    public static async Task<CosmeticEntitlementDto> SeedFragmentEntitlementAsync(
        IServiceProvider services,
        string userId,
        int cosmeticItemId,
        int quantity,
        string sourceType,
        string sourceRef)
    {
        using var scope = services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICosmeticEntitlementService>();
        return await service.CreateFragmentEntitlementAsync(
            userId,
            cosmeticItemId,
            quantity,
            sourceType,
            sourceRef,
            $"test:fragment:{userId}:{cosmeticItemId}:{quantity}:{sourceRef}",
            CancellationToken.None);
    }
}
