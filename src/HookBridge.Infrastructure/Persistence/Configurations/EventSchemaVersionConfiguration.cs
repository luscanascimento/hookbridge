using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class EventSchemaVersionConfiguration : IEntityTypeConfiguration<EventSchemaVersion>
{
    public void Configure(EntityTypeBuilder<EventSchemaVersion> builder)
    {
        builder.ToTable("event_schema_versions");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.TenantId)
            .IsRequired();

        builder.Property(v => v.EventSchemaId)
            .IsRequired();

        builder.Property(v => v.Version)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(v => v.VersionNumber)
            .IsRequired();

        builder.Property(v => v.SchemaJson)
            .IsRequired();

        builder.Property(v => v.Description)
            .HasMaxLength(1024);

        builder.Property(v => v.IsActive)
            .IsRequired();

        builder.Property(v => v.IsDeprecated)
            .IsRequired();

        builder.HasIndex(v => new { v.TenantId, v.EventSchemaId, v.VersionNumber })
            .IsUnique();

        builder.HasIndex(v => new { v.TenantId, v.EventSchemaId, v.Version })
            .IsUnique();

        builder.HasIndex(v => v.TenantId);
        builder.HasIndex(v => v.EventSchemaId);
    }
}
