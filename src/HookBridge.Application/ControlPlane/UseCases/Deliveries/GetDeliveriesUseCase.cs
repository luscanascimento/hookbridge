using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Deliveries;

public sealed class GetDeliveriesUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetDeliveriesUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<PagedList<DeliveryResponse>>> ExecuteAsync(GetDeliveriesQuery query, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<PagedList<DeliveryResponse>>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var baseQuery = _dbContext.Deliveries
            .Where(d => d.TenantId == tenantId);

        if (query.EndpointId.HasValue)
        {
            baseQuery = baseQuery.Where(d => d.EndpointId == query.EndpointId.Value);
        }

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(d => d.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            var eventTypeTrimmed = query.EventType.Trim();
            baseQuery = baseQuery.Where(d => d.EventType == eventTypeTrimmed);
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            var corrTrimmed = query.CorrelationId.Trim();
            baseQuery = baseQuery.Where(d => d.CorrelationId == corrTrimmed);
        }

        if (query.FromDate.HasValue)
        {
            baseQuery = baseQuery.Where(d => d.CreatedAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            baseQuery = baseQuery.Where(d => d.CreatedAt <= query.ToDate.Value);
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await baseQuery
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DeliveryResponse(
                d.Id,
                d.TenantId,
                d.EventId,
                d.EndpointId,
                d.SubscriptionId,
                d.EventType,
                d.Status,
                d.ScheduledAt,
                d.DeliveredAt,
                d.AttemptCount,
                d.TraceParent,
                d.CorrelationId,
                d.OriginalDeliveryId,
                d.CreatedAt,
                d.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedList<DeliveryResponse>(items, page, pageSize, totalCount));
    }
}
