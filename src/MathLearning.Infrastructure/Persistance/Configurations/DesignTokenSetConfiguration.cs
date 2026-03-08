using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class DesignTokenSetConfiguration : IEntityTypeConfiguration<DesignTokenSet>
{
    public void Configure(EntityTypeBuilder<DesignTokenSet> builder)
    {
        builder.ToTable("DesignTokenSet");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Theme).IsRequired().HasMaxLength(64);
        builder.Property(x => x.CompiledPayloadJson).IsRequired().HasColumnType("text");
        builder.Property(x => x.PayloadHash).HasMaxLength(128);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.VersionId, x.Theme })
            .IsUnique()
            .HasDatabaseName("UX_DesignTokenSet_Version_Theme");

        builder.HasIndex(x => new { x.Theme, x.UpdatedAtUtc })
            .HasDatabaseName("IX_DesignTokenSet_Theme_UpdatedAtUtc");

        builder.HasOne(x => x.Version)
            .WithMany(x => x.TokenSets)
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
