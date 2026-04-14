using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MathLearning.Application.DTOs.DesignTokens;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathLearning.Infrastructure.Services.DesignTokens;

public sealed class DesignTokenPlatformService : IDesignTokenQueryService, IDesignTokenAdminService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ApiDbContext dbContext;
    private readonly IDesignTokenMergeService mergeService;
    private readonly IDesignTokenCompilerService compilerService;
    private readonly IDesignTokenCacheService cacheService;
    private readonly IDesignTokenVersionManager versionManager;
    private readonly IDesignTokenAuditService auditService;
    private readonly IOptions<DesignTokenOptions> options;
    private readonly ILogger<DesignTokenPlatformService> logger;

    public DesignTokenPlatformService(
        ApiDbContext dbContext,
        IDesignTokenMergeService mergeService,
        IDesignTokenCompilerService compilerService,
        IDesignTokenCacheService cacheService,
        IDesignTokenVersionManager versionManager,
        IDesignTokenAuditService auditService,
        IOptions<DesignTokenOptions> options,
        ILogger<DesignTokenPlatformService> logger)
    {
        this.dbContext = dbContext;
        this.mergeService = mergeService;
        this.compilerService = compilerService;
        this.cacheService = cacheService;
        this.versionManager = versionManager;
        this.auditService = auditService;
        this.options = options;
        this.logger = logger;
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        var exists = await dbContext.DesignTokenVersions.AnyAsync(
            x => x.IsCurrent,
            cancellationToken);
        if (exists)
        {
            return;
        }

        var version = new DesignTokenVersion
        {
            Version = "1.0.0",
            Status = DesignTokenVersionStatuses.Published,
            BaseWidth = options.Value.BaseWidth > 0 ? options.Value.BaseWidth : 390,
            IsCurrent = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            PublishedAtUtc = DateTime.UtcNow
        };

        foreach (var theme in GetConfiguredThemes())
        {
            var set = new DesignTokenSet
            {
                Theme = theme,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var merged = mergeService.Merge(theme, new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase));
            var compiled = compilerService.Compile(version.Version, version.BaseWidth, theme, merged);
            UpsertSetTokens(set, merged, compiled);
            version.TokenSets.Add(set);
        }

        dbContext.DesignTokenVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var set in version.TokenSets)
        {
            var payload = JsonSerializer.Deserialize<DesignTokensResponse>(set.CompiledPayloadJson, SerializerOptions)!;
            await cacheService.SetAsync(version.Version, set.Theme, payload, cancellationToken);
        }
    }

    public Task<DesignTokensResponse> GetCurrentTokensAsync(string? theme, CancellationToken cancellationToken) =>
        GetCurrentTokensByThemeAsync(theme ?? "light", cancellationToken);

    public async Task<DesignTokensResponse> GetCurrentTokensByThemeAsync(string theme, CancellationToken cancellationToken)
    {
        var current = await GetCurrentPublishedVersionAsync(cancellationToken);
        var normalizedTheme = DesignTokenMergeService.NormalizeTheme(theme);

        var cached = await cacheService.GetAsync(current.Version, normalizedTheme, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var set = await dbContext.DesignTokenSets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.VersionId == current.Id && x.Theme == normalizedTheme, cancellationToken)
            ?? throw new KeyNotFoundException($"Theme '{normalizedTheme}' not found.");

        var response = JsonSerializer.Deserialize<DesignTokensResponse>(set.CompiledPayloadJson, SerializerOptions)
            ?? throw new InvalidOperationException("Compiled payload is invalid.");

        await cacheService.SetAsync(current.Version, normalizedTheme, response, cancellationToken);
        return response;
    }

    public async Task<DesignTokenVersionResponse> GetCurrentVersionAsync(CancellationToken cancellationToken)
    {
        var current = await GetCurrentPublishedVersionAsync(cancellationToken);
        return new DesignTokenVersionResponse(
            current.Version,
            current.BaseWidth,
            current.PublishedAtUtc,
            current.TokenSets.Select(x => x.Theme).OrderBy(x => x).ToArray());
    }

    public async Task<AdminDesignTokensResponse> GetAdminTokensAsync(CancellationToken cancellationToken)
    {
        var versions = await dbContext.DesignTokenVersions
            .AsNoTracking()
            .Include(x => x.TokenSets)
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(4)
            .ToListAsync(cancellationToken);

        var published = versions.FirstOrDefault(x => x.IsCurrent);
        var draft = versions.FirstOrDefault(x => x.Status == DesignTokenVersionStatuses.Draft);
        var effective = draft ?? published ?? throw new InvalidOperationException("No design token version found.");

        var themes = effective.TokenSets.ToDictionary(
            x => x.Theme,
            x => JsonSerializer.Deserialize<DesignTokensResponse>(x.CompiledPayloadJson, SerializerOptions)
                ?? throw new InvalidOperationException("Compiled payload is invalid."),
            StringComparer.OrdinalIgnoreCase);

        return new AdminDesignTokensResponse(
            published is null ? null : new DesignTokenVersionResponse(published.Version, published.BaseWidth, published.PublishedAtUtc, published.TokenSets.Select(x => x.Theme).OrderBy(x => x).ToArray()),
            draft is null ? null : new DesignTokenVersionResponse(draft.Version, draft.BaseWidth, draft.PublishedAtUtc, draft.TokenSets.Select(x => x.Theme).OrderBy(x => x).ToArray()),
            themes);
    }

    public async Task<DesignTokensResponse> UpsertDraftAsync(
        UpsertDesignTokensRequest request,
        string? actorUserId,
        string? actorName,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var theme = DesignTokenMergeService.NormalizeTheme(request.Theme);
        var draft = await EnsureDraftVersionAsync(actorUserId, request.Notes, cancellationToken);
        var set = draft.TokenSets.FirstOrDefault(x => x.Theme == theme);

        if (set is null)
        {
            set = new DesignTokenSet
            {
                Theme = theme,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            draft.TokenSets.Add(set);
        }

        if (request.BaseWidth is { } baseWidth)
        {
            if (baseWidth <= 0)
            {
                throw new InvalidOperationException("BaseWidth must be greater than zero.");
            }

            draft.BaseWidth = baseWidth;
        }

        var beforeSnapshot = set.CompiledPayloadJson;
        var rawTokens = ToRawTokenDictionary(set.Tokens);
        ApplyRequest(rawTokens, request);
        var merged = mergeService.Merge(theme, rawTokens);
        var compiled = compilerService.Compile(draft.Version, draft.BaseWidth, theme, merged);
        UpsertSetTokens(set, merged, compiled);
        draft.Notes = request.Notes ?? draft.Notes;
        draft.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            "DraftUpdated",
            actorUserId,
            actorName,
            correlationId,
            draft.Id,
            set.Id,
            theme,
            beforeSnapshot,
            set.CompiledPayloadJson,
            new { draft.Version, draft.BaseWidth },
            cancellationToken);

        logger.LogInformation("Updated design token draft for {Theme}", theme);
        return compiled;
    }

    public async Task<DesignTokenVersionResponse> PublishDraftAsync(
        PublishDesignTokenVersionRequest request,
        string? actorUserId,
        string? actorName,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var draft = await dbContext.DesignTokenVersions
            .Include(x => x.TokenSets)
            .ThenInclude(x => x.Tokens)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(x => x.Status == DesignTokenVersionStatuses.Draft, cancellationToken)
            ?? throw new InvalidOperationException("No draft version available.");

        var current = await dbContext.DesignTokenVersions
            .Include(x => x.TokenSets)
            .FirstAsync(x => x.IsCurrent, cancellationToken);

        var targetVersion = versionManager.EnsureNextVersion(request.Version, current.Version);
        if (await dbContext.DesignTokenVersions.AnyAsync(x => x.Version == targetVersion && x.Id != draft.Id, cancellationToken))
        {
            throw new InvalidOperationException($"Version '{targetVersion}' already exists.");
        }

        current.IsCurrent = false;
        current.UpdatedAtUtc = DateTime.UtcNow;

        draft.Version = targetVersion;
        draft.Status = DesignTokenVersionStatuses.Published;
        draft.IsCurrent = true;
        draft.Notes = request.Notes ?? draft.Notes;
        draft.PublishedByUserId = actorUserId;
        draft.PublishedAtUtc = DateTime.UtcNow;
        draft.UpdatedAtUtc = DateTime.UtcNow;

        foreach (var set in draft.TokenSets)
        {
            var merged = mergeService.Merge(set.Theme, ToRawTokenDictionary(set.Tokens));
            var compiled = compilerService.Compile(draft.Version, draft.BaseWidth, set.Theme, merged);
            UpsertSetTokens(set, merged, compiled);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var theme in current.TokenSets.Select(x => x.Theme))
        {
            await cacheService.RemoveAsync(current.Version, theme, cancellationToken);
        }

        foreach (var set in draft.TokenSets)
        {
            var payload = JsonSerializer.Deserialize<DesignTokensResponse>(set.CompiledPayloadJson, SerializerOptions)!;
            await cacheService.SetAsync(draft.Version, set.Theme, payload, cancellationToken);
        }

        await auditService.WriteAsync(
            "Published",
            actorUserId,
            actorName,
            correlationId,
            draft.Id,
            null,
            null,
            JsonSerializer.Serialize(new { current.Version, current.BaseWidth }, SerializerOptions),
            JsonSerializer.Serialize(new { draft.Version, draft.BaseWidth }, SerializerOptions),
            new { previousVersion = current.Version, draft.Version },
            cancellationToken);

        return new DesignTokenVersionResponse(
            draft.Version,
            draft.BaseWidth,
            draft.PublishedAtUtc,
            draft.TokenSets.Select(x => x.Theme).OrderBy(x => x).ToArray());
    }

    public async Task<DesignTokenVersionResponse> RollbackAsync(
        string version,
        RollbackDesignTokenVersionRequest request,
        string? actorUserId,
        string? actorName,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var current = await dbContext.DesignTokenVersions
            .Include(x => x.TokenSets)
            .FirstAsync(x => x.IsCurrent, cancellationToken);

        var source = await dbContext.DesignTokenVersions
            .Include(x => x.TokenSets)
            .ThenInclude(x => x.Tokens)
            .FirstOrDefaultAsync(x => x.Version == version && x.Status != DesignTokenVersionStatuses.Draft, cancellationToken)
            ?? throw new KeyNotFoundException($"Version '{version}' not found.");

        var targetVersion = versionManager.CreateRollbackVersion(request.TargetVersion, current.Version);
        if (await dbContext.DesignTokenVersions.AnyAsync(x => x.Version == targetVersion, cancellationToken))
        {
            throw new InvalidOperationException($"Version '{targetVersion}' already exists.");
        }

        var rollback = new DesignTokenVersion
        {
            Version = targetVersion,
            Status = DesignTokenVersionStatuses.RolledBack,
            BaseWidth = source.BaseWidth,
            IsCurrent = true,
            SourceVersionId = source.Id,
            Notes = request.Reason ?? $"Rollback to {version}",
            CreatedByUserId = actorUserId,
            PublishedByUserId = actorUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            PublishedAtUtc = DateTime.UtcNow
        };

        foreach (var sourceSet in source.TokenSets)
        {
            var set = new DesignTokenSet
            {
                Theme = sourceSet.Theme,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var merged = mergeService.Merge(sourceSet.Theme, ToRawTokenDictionary(sourceSet.Tokens));
            var compiled = compilerService.Compile(targetVersion, rollback.BaseWidth, sourceSet.Theme, merged);
            UpsertSetTokens(set, merged, compiled);
            rollback.TokenSets.Add(set);
        }

        current.IsCurrent = false;
        current.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.DesignTokenVersions.Add(rollback);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var theme in current.TokenSets.Select(x => x.Theme))
        {
            await cacheService.RemoveAsync(current.Version, theme, cancellationToken);
        }

        foreach (var set in rollback.TokenSets)
        {
            var payload = JsonSerializer.Deserialize<DesignTokensResponse>(set.CompiledPayloadJson, SerializerOptions)!;
            await cacheService.SetAsync(rollback.Version, set.Theme, payload, cancellationToken);
        }

        await auditService.WriteAsync(
            "RolledBack",
            actorUserId,
            actorName,
            correlationId,
            rollback.Id,
            null,
            null,
            JsonSerializer.Serialize(new { currentVersion = current.Version, sourceVersion = source.Version }, SerializerOptions),
            JsonSerializer.Serialize(new { rollback.Version, rollback.BaseWidth }, SerializerOptions),
            new { request.Reason, sourceVersion = source.Version, targetVersion },
            cancellationToken);

        return new DesignTokenVersionResponse(
            rollback.Version,
            rollback.BaseWidth,
            rollback.PublishedAtUtc,
            rollback.TokenSets.Select(x => x.Theme).OrderBy(x => x).ToArray());
    }

    public async Task<IReadOnlyList<DesignTokenHistoryItemResponse>> GetHistoryAsync(CancellationToken cancellationToken)
    {
        var versions = await dbContext.DesignTokenVersions
            .AsNoTracking()
            .Include(x => x.TokenSets)
            .OrderByDescending(x => x.PublishedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return versions.Select(x => new DesignTokenHistoryItemResponse(
            x.Version,
            x.Status,
            x.BaseWidth,
            x.IsCurrent,
            x.CreatedAtUtc,
            x.PublishedAtUtc,
            x.Notes,
            x.TokenSets.Select(s => s.Theme).OrderBy(s => s).ToArray()))
            .ToArray();
    }

    private async Task<DesignTokenVersion> GetCurrentPublishedVersionAsync(CancellationToken cancellationToken)
    {
        return await dbContext.DesignTokenVersions
            .AsNoTracking()
            .Include(x => x.TokenSets)
            .FirstOrDefaultAsync(x => x.IsCurrent, cancellationToken)
            ?? throw new InvalidOperationException("Current published design token version not found.");
    }

    private async Task<DesignTokenVersion> EnsureDraftVersionAsync(string? actorUserId, string? notes, CancellationToken cancellationToken)
    {
        var draft = await dbContext.DesignTokenVersions
            .Include(x => x.TokenSets)
            .ThenInclude(x => x.Tokens)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(x => x.Status == DesignTokenVersionStatuses.Draft, cancellationToken);

        if (draft is not null)
        {
            return draft;
        }

        var current = await dbContext.DesignTokenVersions
            .Include(x => x.TokenSets)
            .ThenInclude(x => x.Tokens)
            .FirstAsync(x => x.IsCurrent, cancellationToken);

        draft = new DesignTokenVersion
        {
            Version = $"draft-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Status = DesignTokenVersionStatuses.Draft,
            BaseWidth = current.BaseWidth,
            IsCurrent = false,
            SourceVersionId = current.Id,
            Notes = notes,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        foreach (var sourceSet in current.TokenSets)
        {
            var set = new DesignTokenSet
            {
                Theme = sourceSet.Theme,
                CompiledPayloadJson = sourceSet.CompiledPayloadJson,
                PayloadHash = sourceSet.PayloadHash,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            foreach (var token in sourceSet.Tokens)
            {
                set.Tokens.Add(new DesignToken
                {
                    Category = token.Category,
                    TokenKey = token.TokenKey,
                    ValueJson = token.ValueJson,
                    ValueType = token.ValueType,
                    Source = token.Source,
                    SortOrder = token.SortOrder,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }

            draft.TokenSets.Add(set);
        }

        dbContext.DesignTokenVersions.Add(draft);
        await dbContext.SaveChangesAsync(cancellationToken);
        return draft;
    }

    private IEnumerable<string> GetConfiguredThemes()
    {
        var defaults = DesignTokenDefaults.Create().Themes.Keys;
        var overrides = options.Value.Themes.Keys;
        return defaults.Concat(overrides)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(DesignTokenMergeService.NormalizeTheme)
            .OrderBy(x => x);
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>> ToRawTokenDictionary(IEnumerable<DesignToken> tokens)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens.OrderBy(x => x.SortOrder))
        {
            if (!result.TryGetValue(token.Category, out var category))
            {
                category = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result[token.Category] = category;
            }

            category[token.TokenKey] = token.ValueJson;
        }

        return result.ToDictionary(
            x => x.Key,
            x => (IReadOnlyDictionary<string, string>)new ReadOnlyDictionary<string, string>(x.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void ApplyRequest(
        IDictionary<string, IReadOnlyDictionary<string, string>> rawTokens,
        UpsertDesignTokensRequest request)
    {
        OverlayNumeric(rawTokens, DesignTokenCategories.Spacing, request.Spacing);
        OverlayNumeric(rawTokens, DesignTokenCategories.Typography, request.Typography);
        OverlayNumeric(rawTokens, DesignTokenCategories.Radius, request.Radius);
        OverlayString(rawTokens, DesignTokenCategories.Colors, request.Colors);
        OverlayNumeric(rawTokens, DesignTokenCategories.IconSizes, request.IconSizes);
        OverlayNumeric(rawTokens, DesignTokenCategories.MotionDurations, request.MotionDurations);

        if (request.AdditionalCategories is null)
        {
            return;
        }

        foreach (var category in request.AdditionalCategories)
        {
            if (category.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var values = category.Value.EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.GetRawText(), StringComparer.OrdinalIgnoreCase);
            rawTokens[category.Key] = new ReadOnlyDictionary<string, string>(values);
        }
    }

    private static void OverlayNumeric(
        IDictionary<string, IReadOnlyDictionary<string, string>> target,
        string category,
        IDictionary<string, decimal>? values)
    {
        if (values is null || values.Count == 0)
        {
            return;
        }

        var merged = target.TryGetValue(category, out var current)
            ? current.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            merged[value.Key] = JsonSerializer.Serialize(value.Value);
        }

        target[category] = new ReadOnlyDictionary<string, string>(merged);
    }

    private static void OverlayString(
        IDictionary<string, IReadOnlyDictionary<string, string>> target,
        string category,
        IDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return;
        }

        var merged = target.TryGetValue(category, out var current)
            ? current.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            merged[value.Key] = JsonSerializer.Serialize(value.Value);
        }

        target[category] = new ReadOnlyDictionary<string, string>(merged);
    }

    private static void UpsertSetTokens(
        DesignTokenSet set,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> merged,
        DesignTokensResponse compiled)
    {
        set.CompiledPayloadJson = JsonSerializer.Serialize(compiled, SerializerOptions);
        set.PayloadHash = ComputeHash(set.CompiledPayloadJson);
        set.UpdatedAtUtc = DateTime.UtcNow;
        set.Tokens.Clear();

        var order = 0;
        foreach (var category in merged.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var token in category.Value.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                set.Tokens.Add(new DesignToken
                {
                    Category = category.Key,
                    TokenKey = token.Key,
                    ValueJson = token.Value,
                    ValueType = InferValueType(token.Value),
                    Source = "database",
                    SortOrder = order++,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
        }
    }

    private static string InferValueType(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.Number => "number",
            JsonValueKind.String => "string",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            _ => "unknown"
        };
    }

    private static string ComputeHash(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}
