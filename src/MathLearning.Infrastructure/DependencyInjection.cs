using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Cosmetics;
using MathLearning.Infrastructure.Services.DesignTokens;
using MathLearning.Infrastructure.Services.Performance;
using MathLearning.Infrastructure.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace MathLearning.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<HybridCacheService>(sp =>
            new HybridCacheService(
                sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HybridCacheService>>(),
                sp.GetService<IConnectionMultiplexer>()));
        services.Configure<DesignTokenOptions>(config.GetSection("DesignTokens"));
        services.AddScoped<IDesignTokenCacheService, DesignTokenCacheService>();
        services.AddScoped<IDesignTokenMergeService, DesignTokenMergeService>();
        services.AddScoped<IDesignTokenCompilerService, DesignTokenCompilerService>();
        services.AddScoped<IDesignTokenVersionManager, DesignTokenVersionManager>();
        services.AddScoped<IDesignTokenAuditService, DesignTokenAuditService>();
        services.AddScoped<DesignTokenPlatformService>();
        services.AddScoped<IDesignTokenQueryService>(sp => sp.GetRequiredService<DesignTokenPlatformService>());
        services.AddScoped<IDesignTokenAdminService>(sp => sp.GetRequiredService<DesignTokenPlatformService>());
        services.AddScoped<CosmeticPlatformService>();
        services.AddScoped<ICosmeticCatalogService>(sp => sp.GetRequiredService<CosmeticPlatformService>());
        services.AddScoped<ICosmeticInventoryService>(sp => sp.GetRequiredService<CosmeticPlatformService>());
        services.AddScoped<ICosmeticRewardService>(sp => sp.GetRequiredService<CosmeticPlatformService>());
        services.AddScoped<ICosmeticAdminService>(sp => sp.GetRequiredService<CosmeticPlatformService>());
        services.Configure<SyncOptions>(config.GetSection("Sync"));
        services.AddSingleton<SyncMetricsService>();
        services.AddScoped<SyncService>();
        services.AddScoped<ISyncService>(sp => sp.GetRequiredService<SyncService>());
        services.AddScoped<ISyncAdminService>(sp => sp.GetRequiredService<SyncService>());
        services.AddScoped<IOfflineBundleService, OfflineBundleService>();
        services.AddHostedService<SyncDeadLetterRedriveBackgroundService>();
        return services;
    }

    public static IServiceCollection AddApiDatabase(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default");

        services.AddDbContext<ApiDbContext>(opt =>
            opt.UseNpgsql(
                connectionString,
                npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

        return services;
    }

    public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default");

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(connectionString));

        return services;
    }
}
