using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class EventSchemaConfiguration : IEntityTypeConfiguration<EventSchema>
{
    public void Configure(EntityTypeBuilder<EventSchema> builder)
    {
        builder.ToTable("event_schemas");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.Property(s => s.EventType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.Description)
            .HasMaxLength(1024);

        builder.Property(s => s.CompatibilityMode)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.HasIndex(s => new { s.TenantId, s.EventType })
            .IsUnique();

        builder.HasIndex(s => s.TenantId);

        builder.HasMany(s => s.Versions)
            .WithOne(v => v.EventSchema)
            .HasForeignKey(v => v.EventSchemaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
