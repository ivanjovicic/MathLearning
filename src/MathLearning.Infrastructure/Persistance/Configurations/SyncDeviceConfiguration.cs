using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class SyncDeviceConfiguration : IEntityTypeConfiguration<SyncDevice>
{
    public void Configure(EntityTypeBuilder<SyncDevice> builder)
    {
        builder.ToTable("SyncDevices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeviceId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.DeviceName).HasMaxLength(128);
        builder.Property(x => x.Platform).IsRequired().HasMaxLength(32);
        builder.Property(x => x.AppVersion).HasMaxLength(32);
        builder.Property(x => x.SecretKey).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.RegisteredAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastSeenAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.DeviceId)
            .IsUnique()
            .HasDatabaseName("UX_SyncDevices_DeviceId");

        builder.HasIndex(x => new { x.UserId, x.Status })
            .HasDatabaseName("IX_SyncDevices_User_Status");
    }
}
