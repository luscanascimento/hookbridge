using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.TenantId)
            .IsRequired();

        builder.Property(k => k.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(k => k.KeyPrefix)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(k => k.KeyHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(k => k.KeyHash)
            .IsUnique();

        builder.HasIndex(k => k.TenantId);
    }
}
