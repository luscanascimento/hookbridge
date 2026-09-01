using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class EndpointConfiguration : IEntityTypeConfiguration<Endpoint>
{
    public void Configure(EntityTypeBuilder<Endpoint> builder)
    {
        builder.ToTable("endpoints");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.ApplicationId)
            .IsRequired();

        builder.Property(e => e.TargetUrl)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(e => e.Description)
            .HasMaxLength(512);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.DisabledReason)
            .HasMaxLength(512);

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.ApplicationId);

        builder.HasMany(e => e.Secrets)
            .WithOne()
            .HasForeignKey(s => s.EndpointId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Subscriptions)
            .WithOne()
            .HasForeignKey(sub => sub.EndpointId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Deliveries)
            .WithOne()
            .HasForeignKey(d => d.EndpointId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
