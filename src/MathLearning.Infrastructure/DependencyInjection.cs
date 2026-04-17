using MathLearning.Application.Content;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.AntiCheat;
using MathLearning.Infrastructure.Services.Cosmetics;
using MathLearning.Infrastructure.Services.DesignTokens;
using MathLearning.Infrastructure.Services.Performance;
using MathLearning.Infrastructure.Services.QuestionAuthoring;
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
        services.Configure<AntiCheatOptions>(config.GetSection("AntiCheat"));
        services.AddScoped<IAntiCheatMlPromptBuilder, AntiCheatMlPromptBuilder>();
        services.AddScoped<AnswerPatternAntiCheatService>();
        services.AddScoped<IAnswerPatternAntiCheatService>(sp => sp.GetRequiredService<AnswerPatternAntiCheatService>());
        services.AddScoped<IAntiCheatAdminService>(sp => sp.GetRequiredService<AnswerPatternAntiCheatService>());
        services.AddScoped<IAntiCheatMlReviewService>(sp => sp.GetRequiredService<AnswerPatternAntiCheatService>());
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
        services.AddScoped<IMathContentSanitizer, MathContentSanitizer>();
        services.AddScoped<IMathContentLinter, MathContentLinter>();
        services.AddScoped<ILatexValidationService, LatexValidationService>();
        services.AddScoped<IMathNormalizationService, MathNormalizationService>();
        services.AddScoped<IMathEquivalenceService, MathEquivalenceService>();
        services.AddScoped<IStepExplanationValidationService, StepExplanationValidationService>();
        services.AddScoped<IDifficultyEstimationService, DifficultyEstimationService>();
        services.AddScoped<IQuestionPreviewService, QuestionPreviewService>();
        services.AddScoped<IQuestionPublishGuardService, QuestionPublishGuardService>();
        services.AddScoped<IQuestionAutoHintGenerator, NoOpQuestionAutoHintGenerator>();
        services.AddScoped<IQuestionAuthoringService, QuestionAuthoringService>();
        services.AddScoped<MathQuestionAuthoringService>();
        services.AddScoped<IMathQuestionAuthoringService>(sp => sp.GetRequiredService<MathQuestionAuthoringService>());
        services.AddScoped<IMathQuestionValidationService>(sp => sp.GetRequiredService<MathQuestionAuthoringService>());
        services.AddScoped<IQuestionVersioningService>(sp => sp.GetRequiredService<MathQuestionAuthoringService>());
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
