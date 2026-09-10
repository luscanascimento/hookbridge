using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Deliveries;

public sealed class GetDeliveryStatsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetDeliveryStatsUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<DeliveryStatsResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<DeliveryStatsResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        // 1. Efficient GroupBy aggregation on delivery statuses
        var statusCounts = await _dbContext.Deliveries
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var successful = statusCounts.FirstOrDefault(s => s.Status == DeliveryStatus.Success)?.Count ?? 0;
        var failed = statusCounts.FirstOrDefault(s => s.Status == DeliveryStatus.Failed)?.Count ?? 0;
        var pending = statusCounts.Where(s => s.Status == DeliveryStatus.Pending || s.Status == DeliveryStatus.Dispatched).Sum(s => s.Count);
        var deadLettered = statusCounts.FirstOrDefault(s => s.Status == DeliveryStatus.DeadLettered)?.Count ?? 0;
        var total = statusCounts.Sum(s => s.Count);

        var successRate = total > 0 ? Math.Round((double)successful / total * 100, 2) : 100.0;

        // 2. Efficient database-level average latency calculation
        var avgLatencyResult = await _dbContext.Attempts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .Select(a => (double?)a.ElapsedMs)
            .AverageAsync(cancellationToken);

        var avgLatency = avgLatencyResult.HasValue ? Math.Round(avgLatencyResult.Value, 2) : 0.0;

        var now = DateTimeOffset.UtcNow;
        var start24h = now.AddHours(-23);

        // 3. Fast time-series querying constrained to the 24h window
        var recentDeliveries = await _dbContext.Deliveries
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CreatedAt >= start24h)
            .Select(d => new { d.CreatedAt, d.Status })
            .ToListAsync(cancellationToken);

        var recentAttempts = await _dbContext.Attempts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.ExecutedAt >= start24h)
            .Select(a => new { a.ExecutedAt, a.ElapsedMs })
            .ToListAsync(cancellationToken);

        var timeSeries = new List<TimeSeriesBucket>();
        for (int i = 0; i < 24; i++)
        {
            var bucketStart = new DateTimeOffset(start24h.Year, start24h.Month, start24h.Day, start24h.Hour, 0, 0, TimeSpan.Zero).AddHours(i);
            var bucketEnd = bucketStart.AddHours(1);

            var bucketDeliveries = recentDeliveries.Where(d => d.CreatedAt >= bucketStart && d.CreatedAt < bucketEnd).ToList();
            var bucketAttempts = recentAttempts.Where(a => a.ExecutedAt >= bucketStart && a.ExecutedAt < bucketEnd).ToList();

            var bTotal = bucketDeliveries.Count;
            var bSuccess = bucketDeliveries.Count(d => d.Status == DeliveryStatus.Success);
            var bFailed = bucketDeliveries.Count(d => d.Status == DeliveryStatus.Failed);
            var bDeadLettered = bucketDeliveries.Count(d => d.Status == DeliveryStatus.DeadLettered);
            var bAvgLatency = bucketAttempts.Count > 0 ? Math.Round(bucketAttempts.Average(a => a.ElapsedMs), 2) : 0.0;

            timeSeries.Add(new TimeSeriesBucket(bucketStart, bTotal, bSuccess, bFailed, bDeadLettered, bAvgLatency));
        }

        return Result.Success(new DeliveryStatsResponse(
            total,
            successful,
            failed,
            pending,
            deadLettered,
            successRate,
            avgLatency,
            timeSeries));
    }
}
