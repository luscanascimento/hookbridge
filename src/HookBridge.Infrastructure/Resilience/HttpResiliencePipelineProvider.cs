using System.Net;
using HookBridge.Domain.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace HookBridge.Infrastructure.Resilience;

/// <summary>
/// Default implementation of <see cref="IHttpResiliencePipelineProvider"/> using Polly v8.
/// Orchestrates Total Timeout -> Retry (with Jitter & Retry-After) -> Circuit Breaker -> Attempt Timeout.
/// </summary>
public sealed partial class HttpResiliencePipelineProvider : IHttpResiliencePipelineProvider
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly ILogger<HttpResiliencePipelineProvider> _logger;

    public ResiliencePipeline<HttpResponseMessage> Pipeline => _pipeline;

    public HttpResiliencePipelineProvider(
        IOptions<ResilienceOptions> options,
        ILogger<HttpResiliencePipelineProvider> logger)
    {
        _logger = logger;
        var opts = options.Value;

        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        // 1. Outer: Total Request Timeout
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(opts.TotalRequestTimeoutSeconds)
        });

        // 2. Retry with Exponential Backoff + Jitter + Retry-After awareness
        builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = opts.MaxRetryAttempts,
            Delay = TimeSpan.FromMilliseconds(opts.BaseDelayMs),
            MaxDelay = TimeSpan.FromMilliseconds(opts.MaxDelayMs),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = opts.UseJitter,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(response =>
                    (int)response.StatusCode >= 500 ||
                    response.StatusCode == HttpStatusCode.RequestTimeout ||
                    response.StatusCode == HttpStatusCode.TooManyRequests),
            DelayGenerator = args =>
            {
                if (args.Outcome.Result is HttpResponseMessage response &&
                    response.Headers.RetryAfter != null)
                {
                    if (response.Headers.RetryAfter.Delta.HasValue)
                    {
                        return ValueTask.FromResult<TimeSpan?>(response.Headers.RetryAfter.Delta.Value);
                    }
                    if (response.Headers.RetryAfter.Date.HasValue)
                    {
                        var delay = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                        if (delay > TimeSpan.Zero)
                        {
                            return ValueTask.FromResult<TimeSpan?>(delay);
                        }
                    }
                }
                return ValueTask.FromResult<TimeSpan?>(null);
            },
            OnRetry = args =>
            {
                var outcome = args.Outcome.Exception != null
                    ? args.Outcome.Exception.Message
                    : $"HTTP {(int)(args.Outcome.Result?.StatusCode ?? 0)}";

                LogRetryAttempt(_logger, args.AttemptNumber + 1, outcome, args.RetryDelay.TotalMilliseconds);
                return ValueTask.CompletedTask;
            }
        });

        // 3. Circuit Breaker
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = opts.CircuitBreakerFailureRatio,
            SamplingDuration = TimeSpan.FromSeconds(opts.CircuitBreakerSamplingDurationSeconds),
            MinimumThroughput = opts.CircuitBreakerMinimumThroughput,
            BreakDuration = TimeSpan.FromSeconds(opts.CircuitBreakerBreakDurationSeconds),
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(response =>
                    (int)response.StatusCode >= 500 ||
                    response.StatusCode == HttpStatusCode.RequestTimeout),
            OnOpened = args =>
            {
                HookBridgeDiagnostics.ActiveCircuitBreakers.Add(1);
                LogCircuitBreakerOpened(_logger, args.BreakDuration.TotalSeconds);
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                HookBridgeDiagnostics.ActiveCircuitBreakers.Add(-1);
                LogCircuitBreakerClosed(_logger);
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                LogCircuitBreakerHalfOpened(_logger);
                return ValueTask.CompletedTask;
            }
        });

        // 4. Inner: Per-attempt timeout
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(opts.AttemptTimeoutSeconds)
        });

        _pipeline = builder.Build();
    }

    public async Task<HttpResponseMessage> ExecuteAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> action,
        CancellationToken cancellationToken = default)
    {
        return await _pipeline.ExecuteAsync(
            async (state, ct) => await state(ct),
            action,
            cancellationToken);
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning, Message = "HTTP resilience retry attempt {AttemptNumber} due to {Outcome}. Delaying for {DelayMs}ms.")]
    private static partial void LogRetryAttempt(ILogger logger, int attemptNumber, string outcome, double delayMs);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "HTTP circuit breaker OPENED for {BreakDurationSeconds}s due to elevated failure rates.")]
    private static partial void LogCircuitBreakerOpened(ILogger logger, double breakDurationSeconds);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "HTTP circuit breaker CLOSED. Normal outbound traffic restored.")]
    private static partial void LogCircuitBreakerClosed(ILogger logger);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Warning, Message = "HTTP circuit breaker entered HALF-OPEN state. Probing destination health.")]
    private static partial void LogCircuitBreakerHalfOpened(ILogger logger);
}
