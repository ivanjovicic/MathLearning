using System.Text.Json;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;

namespace MathLearning.Infrastructure.Services.DesignTokens;

public sealed class DesignTokenAuditService : IDesignTokenAuditService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ApiDbContext dbContext;

    public DesignTokenAuditService(ApiDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task WriteAsync(
        string action,
        string? actorUserId,
        string? actorName,
        string? correlationId,
        Guid? versionId,
        Guid? tokenSetId,
        string? theme,
        string? beforeSnapshotJson,
        string? afterSnapshotJson,
        object? metadata,
        CancellationToken cancellationToken)
    {
        dbContext.DesignTokenAuditLogs.Add(new DesignTokenAuditLog
        {
            Action = action,
            ActorUserId = actorUserId,
            ActorName = actorName,
            CorrelationId = correlationId,
            VersionId = versionId,
            TokenSetId = tokenSetId,
            Theme = theme,
            BeforeSnapshotJson = beforeSnapshotJson,
            AfterSnapshotJson = afterSnapshotJson,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata, SerializerOptions),
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
