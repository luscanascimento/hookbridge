using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class SimulatorRuleConfiguration : IEntityTypeConfiguration<SimulatorRule>
{
    public void Configure(EntityTypeBuilder<SimulatorRule> builder)
    {
        builder.ToTable("simulator_rules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Strategy)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.TargetStatusCode)
            .IsRequired();

        builder.Property(x => x.SuccessStatusCode)
            .IsRequired();

        builder.Property(x => x.FailureRatePercent)
            .IsRequired();

        builder.Property(x => x.FailureStepCount)
            .IsRequired();

        builder.Property(x => x.CurrentStepCount)
            .IsRequired();

        builder.Property(x => x.DelayMs)
            .IsRequired();

        builder.Property(x => x.MinDelayMs)
            .IsRequired();

        builder.Property(x => x.MaxDelayMs)
            .IsRequired();

        builder.Property(x => x.ResponseHeadersJson)
            .HasMaxLength(4000);

        builder.Property(x => x.ResponseBody)
            .HasMaxLength(65536);

        builder.Property(x => x.ResponseContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.TotalExecutions)
            .IsRequired();

        builder.Property(x => x.TotalFailures)
            .IsRequired();

        builder.Property(x => x.TotalSuccesses)
            .IsRequired();

        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });

        builder.HasMany(x => x.Executions)
            .WithOne(x => x.Rule)
            .HasForeignKey(x => x.RuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
