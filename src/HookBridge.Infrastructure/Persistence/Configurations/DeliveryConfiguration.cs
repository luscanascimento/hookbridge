using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("deliveries");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.TenantId)
            .IsRequired();

        builder.Property(d => d.EventId)
            .IsRequired();

        builder.Property(d => d.EndpointId)
            .IsRequired();

        builder.Property(d => d.SubscriptionId)
            .IsRequired();

        builder.Property(d => d.EventType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(d => d.TraceParent)
            .HasMaxLength(128);

        builder.Property(d => d.CorrelationId)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => d.EventId);
        builder.HasIndex(d => d.EndpointId);
        builder.HasIndex(d => new { d.TenantId, d.Status, d.ScheduledAt });

        builder.HasMany(d => d.Attempts)
            .WithOne()
            .HasForeignKey(a => a.DeliveryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
