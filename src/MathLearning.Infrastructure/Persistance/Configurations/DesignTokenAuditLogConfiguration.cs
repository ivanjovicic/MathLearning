using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class DesignTokenAuditLogConfiguration : IEntityTypeConfiguration<DesignTokenAuditLog>
{
    public void Configure(EntityTypeBuilder<DesignTokenAuditLog> builder)
    {
        builder.ToTable("DesignTokenAuditLog");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Theme).HasMaxLength(64);
        builder.Property(x => x.ActorUserId).HasMaxLength(450);
        builder.Property(x => x.ActorName).HasMaxLength(256);
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.Property(x => x.BeforeSnapshotJson).HasColumnType("text");
        builder.Property(x => x.AfterSnapshotJson).HasColumnType("text");
        builder.Property(x => x.MetadataJson).HasColumnType("text");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.CreatedAtUtc)
            .HasDatabaseName("IX_DesignTokenAuditLog_CreatedAtUtc");

        builder.HasIndex(x => new { x.VersionId, x.CreatedAtUtc })
            .HasDatabaseName("IX_DesignTokenAuditLog_Version_CreatedAtUtc");

        builder.HasIndex(x => new { x.Theme, x.CreatedAtUtc })
            .HasDatabaseName("IX_DesignTokenAuditLog_Theme_CreatedAtUtc");

        builder.HasOne(x => x.Version)
            .WithMany(x => x.AuditLogs)
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.TokenSet)
            .WithMany()
            .HasForeignKey(x => x.TokenSetId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
