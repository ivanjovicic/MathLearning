using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class DeviceSyncStateConfiguration : IEntityTypeConfiguration<DeviceSyncState>
{
    public void Configure(EntityTypeBuilder<DeviceSyncState> builder)
    {
        builder.ToTable("DeviceSyncState");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeviceId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.LastSyncTimeUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastBundleVersion).HasMaxLength(128);

        builder.HasIndex(x => x.DeviceId)
            .IsUnique()
            .HasDatabaseName("UX_DeviceSyncState_DeviceId");

        builder.HasIndex(x => new { x.UserId, x.LastSyncTimeUtc })
            .HasDatabaseName("IX_DeviceSyncState_User_LastSync");
    }
}
