using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Endpoints;

public sealed class GetEndpointHealthMetricsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetEndpointHealthMetricsUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<EndpointHealthResponse>> ExecuteAsync(Guid endpointId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<EndpointHealthResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var now = _dateTimeProvider.UtcNow;
        var windowStart = now.AddHours(-24);

        // 1. Fetch Endpoint
        var endpoint = await _dbContext.Endpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == endpointId && e.TenantId == tenantId, cancellationToken);

        if (endpoint == null)
        {
            return Result.Failure<EndpointHealthResponse>(
                DomainError.NotFound("Endpoint.NotFound", $"Endpoint '{endpointId}' was not found."));
        }

        // 2. Fetch Deliveries & Attempts in the 24-hour window
        var deliveries = await _dbContext.Deliveries
            .AsNoTracking()
            .AsSplitQuery()
            .Include(d => d.Attempts)
            .Where(d => d.EndpointId == endpointId && d.TenantId == tenantId && d.CreatedAt >= windowStart)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        // 3. Aggregate Deliveries Statuses
        var totalDeliveries = deliveries.Count;
        var successCount = deliveries.Count(d => d.Status == DeliveryStatus.Success);
        var failedCount = deliveries.Count(d => d.Status == DeliveryStatus.Failed);
        var deadLetteredCount = deliveries.Count(d => d.Status == DeliveryStatus.DeadLettered);

        var errorCount = failedCount + deadLetteredCount;
        var errorRatePercent = totalDeliveries > 0
            ? Math.Round(((double)errorCount / totalDeliveries) * 100.0, 2)
            : 0.0;

        var uptimePercent = totalDeliveries > 0
            ? Math.Round(((double)successCount / totalDeliveries) * 100.0, 2)
            : (endpoint.Status == EndpointStatus.Active ? 100.0 : 0.0);

        // 4. Extract Attempts & Latency Quantiles
        var allAttempts = deliveries.SelectMany(d => d.Attempts).OrderBy(a => a.ExecutedAt).ToList();
        var latencies = ComputeLatencyQuantiles(allAttempts);

        // 5. Compute Consecutive Failures & Last Failure Reason
        var consecutiveFailures = 0;
        string? lastFailureReason = null;
        DateTimeOffset? lastDeliveryAt = deliveries.LastOrDefault()?.CreatedAt;

        var reverseAttempts = allAttempts.OrderByDescending(a => a.ExecutedAt).ToList();
        foreach (var att in reverseAttempts)
        {
            var isSuccess = att.HttpStatusCode is >= 200 and < 300;
            if (!isSuccess)
            {
                consecutiveFailures++;
                lastFailureReason ??= att.ErrorMessage ?? (att.HttpStatusCode.HasValue ? $"HTTP {att.HttpStatusCode}" : "No response");
            }
            else
            {
                break;
            }
        }

        // 6. Infer Circuit Breaker State
        var circuitState = "Closed";
        if (endpoint.Status == EndpointStatus.Disabled || consecutiveFailures >= 5)
        {
            circuitState = "Open";
        }
        else if (consecutiveFailures >= 3)
        {
            circuitState = "HalfOpen";
        }

        // 7. Calculate Health Score (0 - 100)
        var healthScore = CalculateHealthScore(endpoint.Status, totalDeliveries, successCount, latencies.AverageMs, consecutiveFailures);

        // 8. Generate 24 Hourly Health Buckets
        var hourlyBuckets = GenerateHourlyBuckets(deliveries, windowStart, now);

        // 9. Generate Automated Incident Alerts
        var incidents = GenerateIncidentAlerts(endpoint, circuitState, consecutiveFailures, errorRatePercent, latencies, lastFailureReason, now);

        var response = new EndpointHealthResponse(
            endpoint.Id,
            endpoint.TargetUrl,
            endpoint.Description,
            endpoint.Status.ToString(),
            circuitState,
            healthScore,
            uptimePercent,
            totalDeliveries,
            successCount,
            failedCount,
            deadLetteredCount,
            consecutiveFailures,
            errorRatePercent,
            lastDeliveryAt,
            lastFailureReason,
            latencies,
            hourlyBuckets,
            incidents
        );

        return Result.Success(response);
    }

    public static LatencyQuantilesResponse ComputeLatencyQuantiles(IReadOnlyList<Attempt> attempts)
    {
        if (attempts.Count == 0)
        {
            return new LatencyQuantilesResponse(0, 0, 0, 0, 0, 0, 0);
        }

        var sorted = attempts.Select(a => a.ElapsedMs).OrderBy(x => x).ToList();
        var count = sorted.Count;
        var avg = Math.Round(sorted.Average(), 2);
        var min = sorted[0];
        var max = sorted[^1];

        var p50 = GetPercentile(sorted, 50);
        var p90 = GetPercentile(sorted, 90);
        var p95 = GetPercentile(sorted, 95);
        var p99 = GetPercentile(sorted, 99);

        return new LatencyQuantilesResponse(avg, p50, p90, p95, p99, min, max);
    }

    private static long GetPercentile(List<long> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
        index = Math.Clamp(index, 0, sortedValues.Count - 1);
        return sortedValues[index];
    }

    private static int CalculateHealthScore(EndpointStatus status, int totalDeliveries, int successCount, double avgLatencyMs, int consecutiveFailures)
    {
        if (status == EndpointStatus.Disabled) return 0;
        if (totalDeliveries == 0) return status == EndpointStatus.Active ? 100 : 50;

        var successRate = ((double)successCount / totalDeliveries) * 100.0;
        
        // Latency Score: 100 if <200ms, drops to 0 at 3000ms
        var latencyScore = Math.Clamp(100.0 - ((avgLatencyMs - 200.0) / 28.0), 0.0, 100.0);

        // Penalties: -10 pts per consecutive failure (up to 40 max)
        var failurePenalty = Math.Min(40, consecutiveFailures * 10);

        var score = (successRate * 0.7) + (latencyScore * 0.3) - failurePenalty;
        return (int)Math.Clamp(Math.Round(score), 0, 100);
    }

    private static List<EndpointHourlyHealthBucket> GenerateHourlyBuckets(List<Delivery> deliveries, DateTimeOffset windowStart, DateTimeOffset now)
    {
        var buckets = new List<EndpointHourlyHealthBucket>();
        var currentSlot = new DateTimeOffset(windowStart.Year, windowStart.Month, windowStart.Day, windowStart.Hour, 0, 0, windowStart.Offset);

        while (currentSlot <= now)
        {
            var nextSlot = currentSlot.AddHours(1);
            var slotDeliveries = deliveries.Where(d => d.CreatedAt >= currentSlot && d.CreatedAt < nextSlot).ToList();

            var total = slotDeliveries.Count;
            var success = slotDeliveries.Count(d => d.Status == DeliveryStatus.Success);
            var failed = slotDeliveries.Count(d => d.Status == DeliveryStatus.Failed);
            var dead = slotDeliveries.Count(d => d.Status == DeliveryStatus.DeadLettered);

            var slotAttempts = slotDeliveries.SelectMany(d => d.Attempts).ToList();
            var avgLatency = slotAttempts.Count > 0 ? Math.Round(slotAttempts.Average(a => a.ElapsedMs), 2) : 0;
            var successRate = total > 0 ? Math.Round(((double)success / total) * 100.0, 1) : 100.0;

            var bucketHealth = total > 0
                ? (int)Math.Clamp(Math.Round(successRate * 0.8 + (avgLatency < 500 ? 20 : 0)), 0, 100)
                : 100;

            buckets.Add(new EndpointHourlyHealthBucket(
                currentSlot,
                total,
                success,
                failed,
                dead,
                avgLatency,
                successRate,
                bucketHealth
            ));

            currentSlot = nextSlot;
        }

        return buckets;
    }

    private static List<EndpointIncidentAlert> GenerateIncidentAlerts(
        Endpoint endpoint,
        string circuitState,
        int consecutiveFailures,
        double errorRatePercent,
        LatencyQuantilesResponse latencies,
        string? lastFailureReason,
        DateTimeOffset now)
    {
        var incidents = new List<EndpointIncidentAlert>();

        if (circuitState == "Open")
        {
            incidents.Add(new EndpointIncidentAlert(
                $"inc-circuit-{endpoint.Id:N}",
                "Critical",
                "CircuitBroken",
                "Circuit Breaker Tripped (Open)",
                endpoint.Status == EndpointStatus.Disabled
                    ? $"Endpoint is disabled: {endpoint.DisabledReason ?? "Manual or automatic suspension"}"
                    : $"{consecutiveFailures} consecutive dispatches failed. Traffic has been isolated to protect downstream target.",
                now
            ));
        }
        else if (consecutiveFailures >= 3)
        {
            incidents.Add(new EndpointIncidentAlert(
                $"inc-consecutive-{endpoint.Id:N}",
                "Warning",
                "ConsecutiveFailures",
                "Consecutive Transmission Failures",
                $"{consecutiveFailures} consecutive webhook attempts failed. Last error: '{lastFailureReason ?? "Destination unreachable"}'.",
                now
            ));
        }

        if (errorRatePercent >= 15.0)
        {
            incidents.Add(new EndpointIncidentAlert(
                $"inc-err-{endpoint.Id:N}",
                errorRatePercent >= 40.0 ? "Critical" : "Warning",
                "HighErrorRate",
                "Elevated Delivery Error Rate",
                $"Error rate is currently {errorRatePercent:F1}% over the last 24-hour observation window.",
                now
            ));
        }

        if (latencies.P95Ms > 2500)
        {
            incidents.Add(new EndpointIncidentAlert(
                $"inc-lat-{endpoint.Id:N}",
                "Warning",
                "HighLatency",
                "High p95 Latency Degradation",
                $"95th percentile response latency is {latencies.P95Ms}ms, exceeding SLA threshold.",
                now
            ));
        }

        return incidents;
    }
}
