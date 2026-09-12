using System.ComponentModel.DataAnnotations;

namespace HookBridge.Infrastructure.Resilience;

/// <summary>
/// Configuration options for outbound HTTP resilience policies (Polly v8: Retry, Circuit Breaker, Timeouts).
/// </summary>
public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";

    /// <summary>
    /// Maximum number of retry attempts before giving up.
    /// </summary>
    [Range(1, 10, ErrorMessage = "MaxRetryAttempts must be between 1 and 10.")]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial exponential backoff delay in milliseconds.
    /// </summary>
    [Range(10, 10000, ErrorMessage = "BaseDelayMs must be between 10ms and 10000ms.")]
    public int BaseDelayMs { get; set; } = 200;

    /// <summary>
    /// Maximum delay ceiling for exponential backoff in milliseconds.
    /// </summary>
    [Range(100, 60000, ErrorMessage = "MaxDelayMs must be between 100ms and 60000ms.")]
    public int MaxDelayMs { get; set; } = 5000;

    /// <summary>
    /// The failure ratio (0.0 to 1.0) of requests that will cause the circuit breaker to trip open.
    /// </summary>
    [Range(0.01, 1.0, ErrorMessage = "CircuitBreakerFailureRatio must be between 0.01 and 1.0.")]
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// The rolling sampling duration window for calculating the circuit breaker failure ratio.
    /// </summary>
    [Range(1, 300, ErrorMessage = "CircuitBreakerSamplingDurationSeconds must be between 1 and 300 seconds.")]
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 10;

    /// <summary>
    /// The minimum number of calls that must be made in the sampling duration before the circuit breaker can trip.
    /// </summary>
    [Range(1, 1000, ErrorMessage = "CircuitBreakerMinimumThroughput must be between 1 and 1000.")]
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;

    /// <summary>
    /// How long the circuit breaker stays open before attempting to test the target in half-open state.
    /// </summary>
    [Range(1, 600, ErrorMessage = "CircuitBreakerBreakDurationSeconds must be between 1 and 600 seconds.")]
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout for each individual HTTP request attempt.
    /// </summary>
    [Range(1, 300, ErrorMessage = "AttemptTimeoutSeconds must be between 1 and 300 seconds.")]
    public int AttemptTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Total overall timeout spanning all retry attempts and backoffs.
    /// </summary>
    [Range(1, 600, ErrorMessage = "TotalRequestTimeoutSeconds must be between 1 and 600 seconds.")]
    public int TotalRequestTimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// Whether jitter should be added to retry delays to prevent thundering herd problems.
    /// </summary>
    public bool UseJitter { get; set; } = true;
}
