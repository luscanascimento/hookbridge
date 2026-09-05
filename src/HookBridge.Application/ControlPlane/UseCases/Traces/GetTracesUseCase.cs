using System.Globalization;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Traces;

public sealed class GetTracesUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetTracesUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<PagedList<TraceSummaryResponse>>> ExecuteAsync(TraceQuery query, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<PagedList<TraceSummaryResponse>>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var baseDeliveries = _dbContext.Deliveries
            .AsNoTracking()
            .Include(d => d.Attempts)
            .Where(d => d.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var q = query.Query.Trim();
            baseDeliveries = baseDeliveries.Where(d =>
                d.CorrelationId.Contains(q) ||
                (d.TraceParent != null && d.TraceParent.Contains(q)) ||
                d.EventType.Contains(q) ||
                d.Id.ToString().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<DeliveryStatus>(query.Status, true, out var statusVal))
            {
                baseDeliveries = baseDeliveries.Where(d => d.Status == statusVal);
            }
        }

        if (query.FromDate.HasValue)
        {
            baseDeliveries = baseDeliveries.Where(d => d.CreatedAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            baseDeliveries = baseDeliveries.Where(d => d.CreatedAt <= query.ToDate.Value);
        }

        // Group by CorrelationId (each group represents one distributed transaction trace)
        var allDeliveries = await baseDeliveries
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        var groupedTraces = allDeliveries
            .GroupBy(d => !string.IsNullOrWhiteSpace(d.CorrelationId) ? d.CorrelationId : d.Id.ToString())
            .Select(g =>
            {
                var first = g.First();
                var deliveriesList = g.ToList();
                var allAttempts = deliveriesList.SelectMany(d => d.Attempts).ToList();

                var traceParent = deliveriesList.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.TraceParent))?.TraceParent;
                var traceId = ExtractTraceId(traceParent, first.CorrelationId);

                var initiatedAt = deliveriesList.Min(d => d.CreatedAt);
                var completedAt = deliveriesList.Max(d => d.DeliveredAt ?? d.UpdatedAt);
                var totalDurationMs = CalculateDuration(initiatedAt, completedAt, allAttempts);

                var overallStatus = ResolveOverallStatus(deliveriesList);
                var totalSpans = 4 + deliveriesList.Count + allAttempts.Count; // Ingest + Outbox + Broker + Worker + Deliveries + Attempts

                return new TraceSummaryResponse(
                    traceId,
                    first.CorrelationId,
                    traceParent,
                    first.EventType,
                    initiatedAt,
                    completedAt,
                    totalDurationMs,
                    overallStatus,
                    totalSpans,
                    deliveriesList.Count,
                    allAttempts.Count,
                    1);
            })
            .ToList();

        var totalCount = groupedTraces.Count;
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var pagedItems = groupedTraces
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result.Success(new PagedList<TraceSummaryResponse>(pagedItems, totalCount, page, pageSize));
    }

    private static string ExtractTraceId(string? traceParent, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(traceParent))
        {
            var parts = traceParent.Split('-');
            if (parts.Length >= 4 && parts[1].Length == 32)
            {
                return parts[1];
            }
        }
        return fallback;
    }

    private static long CalculateDuration(DateTimeOffset start, DateTimeOffset? end, List<Attempt> attempts)
    {
        if (attempts.Count > 0)
        {
            var maxAttemptLatency = attempts.Max(a => a.ElapsedMs);
            if (end.HasValue && end.Value > start)
            {
                var diff = (long)(end.Value - start).TotalMilliseconds;
                return Math.Max(diff, maxAttemptLatency + 35);
            }
            return maxAttemptLatency + 40;
        }

        if (end.HasValue && end.Value > start)
        {
            return (long)(end.Value - start).TotalMilliseconds;
        }

        return 12; // Nominal fast ingestion duration ms
    }

    private static string ResolveOverallStatus(List<Delivery> deliveries)
    {
        if (deliveries.Any(d => d.Status == DeliveryStatus.DeadLettered)) return "DeadLettered";
        if (deliveries.Any(d => d.Status == DeliveryStatus.Failed)) return "Failed";
        if (deliveries.All(d => d.Status == DeliveryStatus.Success)) return "Success";
        if (deliveries.Any(d => d.Status == DeliveryStatus.Dispatched)) return "Dispatched";
        return "Pending";
    }
}
