using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathLearning.Infrastructure.Persistance.Configurations;

public sealed class ServerSyncEventConfiguration : IEntityTypeConfiguration<ServerSyncEvent>
{
    public void Configure(EntityTypeBuilder<ServerSyncEvent> builder)
    {
        builder.ToTable("ServerSyncEvent");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.DeviceId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.AggregateType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.AggregateId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.UserId, x.Id })
            .HasDatabaseName("IX_ServerSyncEvent_User_Id");

        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc })
            .HasDatabaseName("IX_ServerSyncEvent_User_CreatedAtUtc");
    }
}
