namespace HookBridge.Domain.Enums;

/// <summary>
/// Failure simulation strategy applied when processing an incoming webhook request.
/// </summary>
public enum SimulatorStrategy
{
    /// <summary>
    /// Always returns the deterministic target HTTP status code.
    /// </summary>
    FixedStatus = 0,

    /// <summary>
    /// Fails a configured percentage of requests (e.g. 30%) with the target error status code,
    /// while the remaining percentage succeeds with the success status code.
    /// </summary>
    FailureRate = 1,

    /// <summary>
    /// Fails the first N requests with the target error status code, then succeeds with 200 OK.
    /// Ideal for verifying retry exponential backoff and recovery.
    /// </summary>
    SequentialRetryPattern = 2,

    /// <summary>
    /// Induces a prolonged latency delay (e.g. 5000ms - 30000ms) or HTTP 504 Gateway Timeout.
    /// </summary>
    Timeout = 3,

    /// <summary>
    /// Injects random latency jitter and randomly picks an error status from a pool (429, 500, 502, 503, 504).
    /// </summary>
    ChaosJitter = 4,

    /// <summary>
    /// Returns corrupted/invalid JSON or unexpected payloads to test client deserialization resilience.
    /// </summary>
    MalformedJson = 5
}
