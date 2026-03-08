using System.Text.Json;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
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
    private readonly ApiDbContext db;
    private readonly ILogger<CosmeticPlatformService> logger;

    public CosmeticPlatformService(ApiDbContext db, ILogger<CosmeticPlatformService> logger)
    {
        this.db = db;
        this.logger = logger;
    }
}
