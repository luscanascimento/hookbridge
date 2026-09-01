using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId)
            .IsRequired();

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(a => a.ResourceType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(a => a.ResourceId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(a => a.DetailsJson)
            .IsRequired();

        builder.Property(a => a.IpAddress)
            .HasMaxLength(64);

        builder.Property(a => a.TraceId)
            .HasMaxLength(128);

        builder.HasIndex(a => new { a.TenantId, a.Timestamp });
    }
}
