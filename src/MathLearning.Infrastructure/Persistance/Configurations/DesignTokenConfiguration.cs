using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class DesignTokenConfiguration : IEntityTypeConfiguration<DesignToken>
{
    public void Configure(EntityTypeBuilder<DesignToken> builder)
    {
        builder.ToTable("DesignToken");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Category).IsRequired().HasMaxLength(128);
        builder.Property(x => x.TokenKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ValueJson).IsRequired().HasColumnType("text");
        builder.Property(x => x.ValueType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(32);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.TokenSetId, x.Category, x.TokenKey })
            .IsUnique()
            .HasDatabaseName("UX_DesignToken_Set_Category_Key");

        builder.HasIndex(x => new { x.Category, x.TokenKey })
            .HasDatabaseName("IX_DesignToken_Category_Key");

        builder.HasOne(x => x.TokenSet)
            .WithMany(x => x.Tokens)
            .HasForeignKey(x => x.TokenSetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
