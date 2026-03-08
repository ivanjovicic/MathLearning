using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class SyncDeadLetterConfiguration : IEntityTypeConfiguration<SyncDeadLetter>
{
    public void Configure(EntityTypeBuilder<SyncDeadLetter> builder)
    {
        builder.ToTable("SyncDeadLetter");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeviceId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.OperationType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.FailureReason).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastFailedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastRedriveAttemptAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ResolvedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ResolutionNote).HasMaxLength(2048);

        builder.HasIndex(x => x.OperationId)
            .IsUnique()
            .HasDatabaseName("UX_SyncDeadLetter_OperationId");

        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc })
            .HasDatabaseName("IX_SyncDeadLetter_User_CreatedAtUtc");

        builder.HasIndex(x => new { x.Status, x.LastFailedAtUtc })
            .HasDatabaseName("IX_SyncDeadLetter_Status_LastFailedAtUtc");
    }
}
