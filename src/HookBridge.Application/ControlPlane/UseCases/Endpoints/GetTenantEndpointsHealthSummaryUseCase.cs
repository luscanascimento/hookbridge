using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Endpoints;

public sealed class GetTenantEndpointsHealthSummaryUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetTenantEndpointsHealthSummaryUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<TenantEndpointsHealthSummaryResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<TenantEndpointsHealthSummaryResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var now = _dateTimeProvider.UtcNow;
        var windowStart = now.AddHours(-24);

        // 1. Fetch all tenant endpoints
        var endpoints = await _dbContext.Endpoints
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        if (endpoints.Count == 0)
        {
            return Result.Success(new TenantEndpointsHealthSummaryResponse(
                100,
                100.0,
                0,
                0,
                0,
                0,
                0,
                0,
                Array.Empty<EndpointHealthSummary>()
            ));
        }

        // 2. Fetch deliveries & attempts within window for all endpoints
        var deliveries = await _dbContext.Deliveries
            .AsNoTracking()
            .Include(d => d.Attempts)
            .Where(d => d.TenantId == tenantId && d.CreatedAt >= windowStart)
            .ToListAsync(cancellationToken);

        var endpointSummaries = new List<EndpointHealthSummary>();
        var totalOpenCircuits = 0;
        var totalActiveIncidents = 0;
        var healthyCount = 0;
        var degradedCount = 0;
        var criticalCount = 0;

        foreach (var ep in endpoints)
        {
            var epDeliveries = deliveries.Where(d => d.EndpointId == ep.Id).ToList();
            var total = epDeliveries.Count;
            var success = epDeliveries.Count(d => d.Status == DeliveryStatus.Success);
            var successRate = total > 0 ? Math.Round(((double)success / total) * 100.0, 2) : 100.0;
            var uptime = total > 0 ? successRate : (ep.Status == EndpointStatus.Active ? 100.0 : 0.0);

            var attempts = epDeliveries.SelectMany(d => d.Attempts).OrderBy(a => a.ExecutedAt).ToList();
            var latencies = GetEndpointHealthMetricsUseCase.ComputeLatencyQuantiles(attempts);

            // Compute consecutive failures
            var consecutiveFailures = 0;
            var revAttempts = attempts.OrderByDescending(a => a.ExecutedAt).ToList();
            foreach (var att in revAttempts)
            {
                if (att.HttpStatusCode is not (>= 200 and < 300))
                {
                    consecutiveFailures++;
                }
                else
                {
                    break;
                }
            }

            // Circuit state
            var circuitState = "Closed";
            if (ep.Status == EndpointStatus.Disabled || consecutiveFailures >= 5)
            {
                circuitState = "Open";
                totalOpenCircuits++;
            }
            else if (consecutiveFailures >= 3)
            {
                circuitState = "HalfOpen";
            }

            // Health Score
            var failurePenalty = Math.Min(40, consecutiveFailures * 10);
            var latencyScore = Math.Clamp(100.0 - ((latencies.AverageMs - 200.0) / 28.0), 0.0, 100.0);
            var calculatedScore = ep.Status == EndpointStatus.Disabled
                ? 0
                : (total == 0
                    ? (ep.Status == EndpointStatus.Active ? 100 : 50)
                    : (int)Math.Clamp(Math.Round((successRate * 0.7) + (latencyScore * 0.3) - failurePenalty), 0, 100));

            // Incident count
            var incidentCount = 0;
            if (circuitState == "Open") incidentCount++;
            if (consecutiveFailures >= 3) incidentCount++;
            if (total > 0 && (100.0 - successRate) >= 15.0) incidentCount++;
            if (latencies.P95Ms > 2500) incidentCount++;

            totalActiveIncidents += incidentCount;

            if (calculatedScore >= 90) healthyCount++;
            else if (calculatedScore >= 60) degradedCount++;
            else criticalCount++;

            endpointSummaries.Add(new EndpointHealthSummary(
                ep.Id,
                ep.TargetUrl,
                ep.Status.ToString(),
                circuitState,
                calculatedScore,
                uptime,
                total,
                successRate,
                latencies.P95Ms,
                incidentCount
            ));
        }

        var overallHealthScore = endpointSummaries.Count > 0
            ? (int)Math.Round(endpointSummaries.Average(e => e.HealthScorePercent))
            : 100;

        var overallUptime = endpointSummaries.Count > 0
            ? Math.Round(endpointSummaries.Average(e => e.UptimePercent), 2)
            : 100.0;

        var response = new TenantEndpointsHealthSummaryResponse(
            overallHealthScore,
            overallUptime,
            endpoints.Count,
            healthyCount,
            degradedCount,
            criticalCount,
            totalOpenCircuits,
            totalActiveIncidents,
            endpointSummaries
        );

        return Result.Success(response);
    }
}
