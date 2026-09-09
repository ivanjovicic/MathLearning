using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.DesignTokens;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace MathLearning.Tests.Services;

public sealed class DesignTokenDraftVersionTests
{
    [Fact]
    public void CreateDraftVersionIdentity_IsUniqueWithinSameSecond_AndFitsMaxLength()
    {
        var manager = new DesignTokenVersionManager();
        var stamp = new DateTime(2026, 8, 3, 12, 34, 56, DateTimeKind.Utc);
        var identities = Enumerable.Range(0, 64)
            .Select(_ => manager.CreateDraftVersionIdentity(stamp))
            .ToArray();

        Assert.Equal(64, identities.Distinct(StringComparer.Ordinal).Count());
        Assert.All(identities, id =>
        {
            Assert.StartsWith("draft-20260803123456-", id, StringComparison.Ordinal);
            Assert.True(id.Length <= 32, id);
            Assert.Matches("^draft-\\d{14}-[a-f0-9]{8}$", id);
        });
    }

    [Fact]
    public void CreateDraftVersionIdentity_ReplacesTimestampOnlyCollisionPattern()
    {
        var manager = new DesignTokenVersionManager();
        var stamp = new DateTime(2026, 8, 3, 12, 34, 56, DateTimeKind.Utc);
        var legacy = $"draft-{stamp:yyyyMMddHHmmss}";
        var modern = manager.CreateDraftVersionIdentity(stamp);

        Assert.Equal(20, legacy.Length);
        Assert.NotEqual(legacy, modern);
        Assert.StartsWith(legacy + "-", modern, StringComparison.Ordinal);
        Assert.True(modern.Length <= 32);
    }

    [Fact]
    public async Task ConcurrentUniqueDraftIdentities_CanBePersistedSideBySide()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var manager = new DesignTokenVersionManager();
        var stamp = DateTime.UtcNow;
        var a = new DesignTokenVersion
        {
            Version = manager.CreateDraftVersionIdentity(stamp),
            Status = DesignTokenVersionStatuses.Draft,
            IsCurrent = false,
            CreatedAtUtc = stamp,
            UpdatedAtUtc = stamp
        };
        var b = new DesignTokenVersion
        {
            Version = manager.CreateDraftVersionIdentity(stamp),
            Status = DesignTokenVersionStatuses.Draft,
            IsCurrent = false,
            CreatedAtUtc = stamp,
            UpdatedAtUtc = stamp
        };

        Assert.NotEqual(a.Version, b.Version);
        db.DesignTokenVersions.AddRange(a, b);
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.DesignTokenVersions.CountAsync(x => x.Status == DesignTokenVersionStatuses.Draft));
    }

    [Fact]
    public async Task EnsureDraftVersion_UsesCollisionSafeIdentity_AndReusesExistingDraft()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateService(db);
        await service.EnsureInitializedAsync(CancellationToken.None);

        var method = typeof(DesignTokenPlatformService).GetMethod(
            "EnsureDraftVersionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var first = await (Task<DesignTokenVersion>)method!.Invoke(
            service,
            ["admin", "notes", CancellationToken.None])!;

        Assert.Matches("^draft-\\d{14}-[a-f0-9]{8}$", first.Version);
        Assert.True(first.Version.Length <= 32);
        Assert.Equal(DesignTokenVersionStatuses.Draft, first.Status);

        var second = await (Task<DesignTokenVersion>)method.Invoke(
            service,
            ["admin", "notes-2", CancellationToken.None])!;

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Version, second.Version);
        Assert.Equal(1, await db.DesignTokenVersions.CountAsync(x => x.Status == DesignTokenVersionStatuses.Draft));
    }

    private static DesignTokenPlatformService CreateService(ApiDbContext db)
    {
        var options = Options.Create(new DesignTokenOptions());
        var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        return new DesignTokenPlatformService(
            db,
            new DesignTokenMergeService(options),
            new DesignTokenCompilerService(),
            new DesignTokenCacheService(memoryCache, options),
            new DesignTokenVersionManager(),
            new DesignTokenAuditService(db),
            options,
            NullLogger<DesignTokenPlatformService>.Instance);
    }
}
