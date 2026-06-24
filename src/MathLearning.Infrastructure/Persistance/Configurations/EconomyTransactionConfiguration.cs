using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class EconomyTransactionConfiguration : IEntityTypeConfiguration<EconomyTransaction>
{
    public void Configure(EntityTypeBuilder<EconomyTransaction> builder)
    {
        builder.ToTable("economy_transactions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.OperationId)
            .HasMaxLength(128);

        builder.Property(x => x.TransactionType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.RequestHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.RequestJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.ResultJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(64);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CompletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.UserId, x.TransactionType, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UX_economy_transactions_user_type_key");

        builder.HasIndex(x => new { x.UserId, x.TransactionType, x.OperationId })
            .IsUnique()
            .HasDatabaseName("UX_economy_transactions_user_type_operation")
            .HasFilter("\"OperationId\" IS NOT NULL");

        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc })
            .HasDatabaseName("IX_economy_transactions_user_created_at");
    }
}
