using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class AdminEconomyRewardGrantConfiguration : IEntityTypeConfiguration<AdminEconomyRewardGrant>
{
    public void Configure(EntityTypeBuilder<AdminEconomyRewardGrant> builder)
    {
        builder.ToTable("admin_economy_reward_grants");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.GrantId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.ActorUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.MetadataJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.UserId, x.GrantId })
            .IsUnique()
            .HasDatabaseName("UX_admin_economy_reward_grants_user_grant");

        builder.HasIndex(x => x.EconomyTransactionId)
            .IsUnique()
            .HasDatabaseName("UX_admin_economy_reward_grants_transaction");

        builder.HasIndex(x => new { x.ActorUserId, x.CreatedAtUtc })
            .HasDatabaseName("IX_admin_economy_reward_grants_actor_created_at");
    }
}