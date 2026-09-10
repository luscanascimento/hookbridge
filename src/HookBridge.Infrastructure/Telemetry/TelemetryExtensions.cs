using HookBridge.Application.Abstractions;
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Singleton instance managed for application lifetime in DI")]
    public static IServiceCollection AddHookBridgeTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var inMemoryBuffer = new InMemoryTelemetryBuffer();
        services.AddSingleton<ITelemetryBuffer>(inMemoryBuffer);
        services.AddSingleton<IObservabilityOptions>(new ObservabilityOptions(configuration));

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: HookBridgeDiagnostics.ServiceName,
                serviceVersion: HookBridgeDiagnostics.Version)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development",
                ["host.name"] = Environment.MachineName,
                ["telemetry.sdk.language"] = "dotnet"
            });

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(HookBridgeDiagnostics.DiagnosticSourceName)
                    .AddHttpClientInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                    })
                    .AddNpgsql()
                    .AddProcessor(inMemoryBuffer);

                // Sampling configuration
                if (double.TryParse(configuration["OpenTelemetry:SamplingRatio"], out var ratio) && ratio is >= 0.0 and < 1.0)
                {
                    builder.SetSampler(new TraceIdRatioBasedSampler(ratio));
                }

                var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    builder.AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(otlpEndpoint);
                    });
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
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    builder.AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(otlpEndpoint);
                    });
                }
            });

        return services;
    }
}
