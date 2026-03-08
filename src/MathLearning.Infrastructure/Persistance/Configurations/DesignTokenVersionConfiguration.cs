using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class DesignTokenVersionConfiguration : IEntityTypeConfiguration<DesignTokenVersion>
{
    public void Configure(EntityTypeBuilder<DesignTokenVersion> builder)
    {
        builder.ToTable("DesignTokenVersion");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Version).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.BaseWidth).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2048);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450);
        builder.Property(x => x.PublishedByUserId).HasMaxLength(450);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.PublishedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.Version)
            .IsUnique()
            .HasDatabaseName("UX_DesignTokenVersion_Version");

        builder.HasIndex(x => x.IsCurrent)
            .HasFilter("\"IsCurrent\" = TRUE")
            .IsUnique()
            .HasDatabaseName("UX_DesignTokenVersion_Current");

        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc })
            .HasDatabaseName("IX_DesignTokenVersion_Status_CreatedAtUtc");

        builder.HasOne(x => x.SourceVersion)
            .WithMany()
            .HasForeignKey(x => x.SourceVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
