using System.Text.Json;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.Cosmetics;

public sealed partial class CosmeticPlatformService :
    ICosmeticCatalogService,
    ICosmeticInventoryService,
    ICosmeticRewardService,
    ICosmeticAdminService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CatalogCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RewardRulesCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AppearanceCacheTtl = TimeSpan.FromSeconds(30);
    private readonly ApiDbContext db;
    private readonly ILogger<CosmeticPlatformService> logger;
    private readonly HybridCacheService cache;

    public CosmeticPlatformService(ApiDbContext db, ILogger<CosmeticPlatformService> logger, HybridCacheService cache)
    {
        this.db = db;
        this.logger = logger;
        this.cache = cache;
    }
}
