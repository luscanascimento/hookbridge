namespace HookBridge.Application.Abstractions;

public interface IObservabilityOptions
{
    string? OtlpEndpoint { get; }
    string EnvironmentName { get; }
    double SamplingRatio { get; }
}
