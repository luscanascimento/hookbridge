using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookBridge.Infrastructure.Persistence.Configurations;

public sealed class SimulatorExecutionConfiguration : IEntityTypeConfiguration<SimulatorExecution>
{
    public void Configure(EntityTypeBuilder<SimulatorExecution> builder)
    {
        builder.ToTable("simulator_executions");

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
            .HasMaxLength(65536);

        builder.Property(x => x.ContentType)
            .HasMaxLength(100);

        builder.Property(x => x.ClientIp)
            .HasMaxLength(45);

        builder.Property(x => x.InjectedFault)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.SimulatedStatusCode)
            .IsRequired();

        builder.Property(x => x.SimulatedDelayMs)
            .IsRequired();

        builder.Property(x => x.SimulatedHeadersJson)
            .HasMaxLength(4000);

        builder.Property(x => x.SimulatedResponseBody)
            .HasMaxLength(65536);

        builder.Property(x => x.ExecutionDurationMs)
            .IsRequired();

        builder.Property(x => x.ExecutedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.ExecutedAt });
        builder.HasIndex(x => new { x.RuleId, x.ExecutedAt });
    }
}
