using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class SyncEventLogConfiguration : IEntityTypeConfiguration<SyncEventLog>
{
    public void Configure(EntityTypeBuilder<SyncEventLog> builder)
    {
        builder.ToTable("SyncEventLog");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.OperationId).IsRequired();
        builder.Property(x => x.DeviceId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.OperationType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ErrorCode).HasMaxLength(64);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2048);
        builder.Property(x => x.OccurredAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ReceivedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ProcessedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.OperationId)
            .IsUnique()
            .HasDatabaseName("UX_SyncEventLog_OperationId");

        builder.HasIndex(x => new { x.DeviceId, x.ClientSequence })
            .IsUnique()
            .HasDatabaseName("UX_SyncEventLog_Device_Sequence");

        builder.HasIndex(x => new { x.UserId, x.Id })
            .HasDatabaseName("IX_SyncEventLog_User_Id");

        builder.HasIndex(x => new { x.Status, x.ReceivedAtUtc })
            .HasDatabaseName("IX_SyncEventLog_Status_ReceivedAtUtc");
    }
}
