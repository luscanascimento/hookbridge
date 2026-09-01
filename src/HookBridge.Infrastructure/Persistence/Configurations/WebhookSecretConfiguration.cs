using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class WebhookSecretConfiguration : IEntityTypeConfiguration<WebhookSecret>
{
    public void Configure(EntityTypeBuilder<WebhookSecret> builder)
    {
        builder.ToTable("webhook_secrets");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.Property(s => s.EndpointId)
            .IsRequired();

        builder.Property(s => s.KeyPrefix)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(s => s.SecretHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(s => s.EncryptedSecret)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.HasIndex(s => new { s.EndpointId, s.Version })
            .IsUnique();

        builder.HasIndex(s => s.TenantId);
    }
}
