using HookBridge.Domain.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HookBridge.Infrastructure.Telemetry;

public static class TelemetryExtensions
{
    public static IServiceCollection AddHookBridgeTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: HookBridgeDiagnostics.ServiceName,
                serviceVersion: HookBridgeDiagnostics.Version);

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(HookBridgeDiagnostics.DiagnosticSourceName)
                    .AddHttpClientInstrumentation()
                    .AddNpgsql();

                var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    builder.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
                }
                else
                {
                    builder.AddConsoleExporter();
                }
            })
            .WithMetrics(builder =>
            {
                builder
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(HookBridgeDiagnostics.DiagnosticSourceName)
                    .AddHttpClientInstrumentation();

                var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    builder.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return services;
    }
}
