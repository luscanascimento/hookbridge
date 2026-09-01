using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        builder.ToTable("attempts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.DeliveryId)
            .IsRequired();

        builder.Property(a => a.TenantId)
            .IsRequired();

        builder.Property(a => a.RequestHeadersJson)
            .IsRequired();

        builder.Property(a => a.RequestBody)
            .IsRequired();

        builder.HasIndex(a => a.DeliveryId);
        builder.HasIndex(a => a.TenantId);
    }
}
