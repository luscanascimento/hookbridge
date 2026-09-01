using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.Property(s => s.EndpointId)
            .IsRequired();

        builder.Property(s => s.EventTypePattern)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(s => new { s.EndpointId, s.EventTypePattern });
        builder.HasIndex(s => new { s.TenantId, s.EventTypePattern });
    }
}
