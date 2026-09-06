using System.Text.Json.Serialization;

namespace HookBridge.Application.ControlPlane.DTOs;

public sealed record LatencyQuantilesResponse(
    [property: JsonPropertyName("averageMs")] double AverageMs,
    [property: JsonPropertyName("p50Ms")] long P50Ms,
    [property: JsonPropertyName("p90Ms")] long P90Ms,
    [property: JsonPropertyName("p95Ms")] long P95Ms,
    [property: JsonPropertyName("p99Ms")] long P99Ms,
    [property: JsonPropertyName("minMs")] long MinMs,
    [property: JsonPropertyName("maxMs")] long MaxMs
);

public sealed record EndpointHourlyHealthBucket(
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("totalDeliveries")] int TotalDeliveries,
    [property: JsonPropertyName("successCount")] int SuccessCount,
    [property: JsonPropertyName("failedCount")] int FailedCount,
    [property: JsonPropertyName("deadLetteredCount")] int DeadLetteredCount,
    [property: JsonPropertyName("averageLatencyMs")] double AverageLatencyMs,
    [property: JsonPropertyName("successRatePercent")] double SuccessRatePercent,
    [property: JsonPropertyName("healthScore")] int HealthScore
);

public sealed record EndpointIncidentAlert(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("severity")] string Severity, // "Critical", "Warning", "Info"
    [property: JsonPropertyName("type")] string Type,         // "CircuitBroken", "HighErrorRate", "HighLatency", "ConsecutiveFailures", "RateLimited"
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("triggeredAt")] DateTimeOffset TriggeredAt
);

public sealed record EndpointHealthResponse(
    [property: JsonPropertyName("endpointId")] Guid EndpointId,
    [property: JsonPropertyName("targetUrl")] string TargetUrl,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("circuitState")] string CircuitState, // "Closed", "HalfOpen", "Open"
    [property: JsonPropertyName("healthScorePercent")] int HealthScorePercent,
    [property: JsonPropertyName("uptimePercent")] double UptimePercent,
    [property: JsonPropertyName("totalDeliveries")] int TotalDeliveries,
    [property: JsonPropertyName("successCount")] int SuccessCount,
    [property: JsonPropertyName("failedCount")] int FailedCount,
    [property: JsonPropertyName("deadLetteredCount")] int DeadLetteredCount,
    [property: JsonPropertyName("consecutiveFailures")] int ConsecutiveFailures,
    [property: JsonPropertyName("errorRatePercent")] double ErrorRatePercent,
    [property: JsonPropertyName("lastDeliveryAt")] DateTimeOffset? LastDeliveryAt,
    [property: JsonPropertyName("lastFailureReason")] string? LastFailureReason,
    [property: JsonPropertyName("latencies")] LatencyQuantilesResponse Latencies,
    [property: JsonPropertyName("hourlyBuckets")] IReadOnlyList<EndpointHourlyHealthBucket> HourlyBuckets,
    [property: JsonPropertyName("incidents")] IReadOnlyList<EndpointIncidentAlert> Incidents
);

public sealed record EndpointHealthSummary(
    [property: JsonPropertyName("endpointId")] Guid EndpointId,
    [property: JsonPropertyName("targetUrl")] string TargetUrl,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("circuitState")] string CircuitState,
    [property: JsonPropertyName("healthScorePercent")] int HealthScorePercent,
    [property: JsonPropertyName("uptimePercent")] double UptimePercent,
    [property: JsonPropertyName("totalDeliveries")] int TotalDeliveries,
    [property: JsonPropertyName("successRatePercent")] double SuccessRatePercent,
    [property: JsonPropertyName("p95LatencyMs")] long P95LatencyMs,
    [property: JsonPropertyName("activeIncidentCount")] int ActiveIncidentCount
);

public sealed record TenantEndpointsHealthSummaryResponse(
    [property: JsonPropertyName("overallHealthScorePercent")] int OverallHealthScorePercent,
    [property: JsonPropertyName("overallUptimePercent")] double OverallUptimePercent,
    [property: JsonPropertyName("totalEndpoints")] int TotalEndpoints,
    [property: JsonPropertyName("healthyCount")] int HealthyCount,     // Score >= 90
    [property: JsonPropertyName("degradedCount")] int DegradedCount,   // 60 <= Score < 90
    [property: JsonPropertyName("criticalCount")] int CriticalCount,   // Score < 60
    [property: JsonPropertyName("totalOpenCircuits")] int TotalOpenCircuits,
    [property: JsonPropertyName("activeIncidentCount")] int ActiveIncidentCount,
    [property: JsonPropertyName("endpoints")] IReadOnlyList<EndpointHealthSummary> Endpoints
);
