using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Cosmetics;
using MathLearning.Infrastructure.Services.Performance;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MathLearning.Tests.Services;

public sealed class CosmeticCatalogImportTests
{
    [Fact]
    public async Task ApplyCurrentManifest_CreatesRevision_AndMakesCatalogReady()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();

        await using var db = new ApiDbContext(database.CreateApiOptions());
        var service = CreateService(db);

        var before = await service.GetCatalogReadinessAsync(CancellationToken.None);
        Assert.False(before.IsReady);
        Assert.Equal("CosmeticCatalogRevisionMissing", before.Reason);

        var import = await service.ApplyCatalogManifestAsync(CancellationToken.None);

        Assert.True(import.Applied);
        Assert.False(import.AlreadyInstalled);
        Assert.Equal(1, import.RevisionCount);

        var after = await service.GetCatalogReadinessAsync(CancellationToken.None);
        Assert.True(after.IsReady);
        Assert.Equal(CosmeticCatalogManifestProvider.Current.RevisionKey, after.RevisionKey);
        Assert.Equal(CosmeticCatalogManifestProvider.Current.RevisionKey, after.CatalogVersion);

        var revisionCount = await db.CosmeticCatalogRevisions.CountAsync();
        Assert.Equal(1, revisionCount);
        Assert.True(await db.CosmeticItems.CountAsync() > 0);
    }

    [Fact]
    public async Task ReapplyingSameManifest_IsNoOp_AndPreservesOperatorManagedFields()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();

        await using var db = new ApiDbContext(database.CreateApiOptions());
        var service = CreateService(db);

        await service.ApplyCatalogManifestAsync(CancellationToken.None);

        var item = await db.CosmeticItems.SingleAsync(x => x.Key == "frame_comet");
        item.Name = "Operator Renamed Frame";
        item.CoinPrice = 777;
        await db.SaveChangesAsync();

        var replay = await service.ApplyCatalogManifestAsync(CancellationToken.None);

        Assert.True(replay.AlreadyInstalled);
        Assert.False(replay.Applied);

        var preserved = await db.CosmeticItems.AsNoTracking().SingleAsync(x => x.Key == "frame_comet");
        Assert.Equal("Operator Renamed Frame", preserved.Name);
        Assert.Equal(777, preserved.CoinPrice);
        Assert.Equal(1, await db.CosmeticCatalogRevisions.CountAsync());
    }

    [Fact]
    public async Task ExplicitDeployManagedUpdate_WritesNewRevision_AndUpdatesFragmentLabel()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();

        await using var db = new ApiDbContext(database.CreateApiOptions());
        var service = CreateService(db);

        await service.ApplyCatalogManifestAsync(CancellationToken.None);

        var manifest = CosmeticCatalogManifestProvider.Create(
            "catalog-20260716-019b",
            CosmeticCatalogManifestProvider.Current.UpsertSql.Replace(
                "Comet Frame Fragment",
                "Comet Frame Fragment Plus",
                StringComparison.Ordinal),
            CosmeticCatalogManifestProvider.Current.ReleaseDateUtc,
            CosmeticCatalogManifestProvider.Current.RequiredDefaultKeys,
            [
                new CosmeticCatalogFragmentRequirement("frame_comet", "Comet Frame Fragment Plus", 5),
                CosmeticCatalogManifestProvider.Current.RequiredFragments[1],
                CosmeticCatalogManifestProvider.Current.RequiredFragments[2]
            ]);

        var update = await service.ApplyCatalogManifestAsync(manifest, CancellationToken.None);

        Assert.True(update.Applied);
        Assert.False(update.AlreadyInstalled);
        Assert.Equal(2, update.RevisionCount);

        var updated = await db.CosmeticItems.AsNoTracking().SingleAsync(x => x.Key == "frame_comet");
        Assert.Equal("Comet Frame Fragment Plus", updated.FragmentLabel);
        Assert.Equal(2, await db.CosmeticCatalogRevisions.CountAsync());
    }

    [Fact]
    public async Task ConcurrentApplyAttempts_ResultInOneAppliedAndOneNoOp()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await database.MigrateApiAsync();

        await using var db1 = new ApiDbContext(database.CreateApiOptions());
        await using var db2 = new ApiDbContext(database.CreateApiOptions());
        var service1 = CreateService(db1);
        var service2 = CreateService(db2);

        var manifest = CosmeticCatalogManifestProvider.Current;
        var first = service1.ApplyCatalogManifestAsync(manifest, CancellationToken.None);
        var second = service2.ApplyCatalogManifestAsync(manifest, CancellationToken.None);

        var results = await Task.WhenAll(first, second);

        Assert.Contains(results, x => x.Applied);
        Assert.Contains(results, x => x.AlreadyInstalled);
        Assert.Equal(1, await db1.CosmeticCatalogRevisions.CountAsync());
    }

    private static CosmeticPlatformService CreateService(ApiDbContext db)
        => new(
            db,
            NullLogger<CosmeticPlatformService>.Instance,
            new HybridCacheService(
                new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 }),
                NullLogger<HybridCacheService>.Instance),
            new AvatarAppearanceReader(db));
}
