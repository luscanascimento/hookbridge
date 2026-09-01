using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainApp = HookBridge.Domain.Entities.Application;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<DomainApp>
{
    public void Configure(EntityTypeBuilder<DomainApp> builder)
    {
        builder.ToTable("applications");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId)
            .IsRequired();

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(a => a.Description)
            .HasMaxLength(512);

        builder.HasIndex(a => new { a.TenantId, a.Name });

        builder.HasMany(a => a.Endpoints)
            .WithOne()
            .HasForeignKey(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
