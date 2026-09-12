using Polly;

namespace HookBridge.Infrastructure.Resilience;

/// <summary>
/// Provides configured Polly v8 resilience pipelines for outbound HTTP calls (EventFlow data plane, webhooks, etc.).
/// Guarantees timeouts, exponential backoff with jitter, retry-after awareness, and circuit breaker protection.
/// </summary>
public interface IHttpResiliencePipelineProvider
{
    /// <summary>
    /// Gets the shared HTTP resilience pipeline.
    /// </summary>
    ResiliencePipeline<HttpResponseMessage> Pipeline { get; }

    /// <summary>
    /// Executes an HTTP request delegate with full resilience guarantees.
    /// The delegate must instantiate a fresh <see cref="HttpRequestMessage"/> per invocation.
    /// </summary>
    Task<HttpResponseMessage> ExecuteAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> action,
        CancellationToken cancellationToken = default);
}
