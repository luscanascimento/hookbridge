using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class WebhookSandboxConfiguration : IEntityTypeConfiguration<WebhookSandbox>
{
    public void Configure(EntityTypeBuilder<WebhookSandbox> builder)
    {
        builder.ToTable("webhook_sandboxes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DefaultResponseStatusCode)
            .IsRequired();

        builder.Property(x => x.DefaultResponseBody)
            .HasMaxLength(65536);

        builder.Property(x => x.DefaultResponseContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DefaultResponseDelayMs)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });

        builder.HasMany(x => x.Requests)
            .WithOne(x => x.Sandbox)
            .HasForeignKey(x => x.SandboxId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
