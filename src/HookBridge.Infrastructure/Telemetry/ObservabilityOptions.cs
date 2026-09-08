using HookBridge.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace HookBridge.Infrastructure.Telemetry;

public sealed class ObservabilityOptions : IObservabilityOptions
{
    public string? OtlpEndpoint { get; }
    public string EnvironmentName { get; }
    public double SamplingRatio { get; }

    public ObservabilityOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        OtlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
        EnvironmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        
        if (double.TryParse(configuration["OpenTelemetry:SamplingRatio"], out var ratio))
        {
            SamplingRatio = Math.Clamp(ratio, 0.0, 1.0);
        }
        else
        {
            SamplingRatio = 1.0;
        }
    }
}
