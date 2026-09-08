using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class SandboxRequestConfiguration : IEntityTypeConfiguration<SandboxRequest>
{
    public void Configure(EntityTypeBuilder<SandboxRequest> builder)
    {
        builder.ToTable("sandbox_requests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.HttpMethod)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.Path)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.QueryString)
            .HasMaxLength(2000);

        builder.Property(x => x.HeadersJson)
            .IsRequired();

        builder.Property(x => x.Body)
            .HasMaxLength(1048576); // 1MB payload capture limit

        builder.Property(x => x.ContentType)
            .HasMaxLength(200);

        builder.Property(x => x.ClientIp)
            .HasMaxLength(50);

        builder.Property(x => x.ResponseStatusCode)
            .IsRequired();

        builder.Property(x => x.ResponseDelayMs)
            .IsRequired();

        builder.Property(x => x.ReceivedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.SandboxId, x.ReceivedAt });
        builder.HasIndex(x => new { x.TenantId, x.ReceivedAt });
    }
}
