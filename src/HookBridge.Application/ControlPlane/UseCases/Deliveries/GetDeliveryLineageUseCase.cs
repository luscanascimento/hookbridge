using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Deliveries;

public sealed class GetDeliveryLineageUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetDeliveryLineageUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<DeliveryLineageResponse>> ExecuteAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<DeliveryLineageResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var initialDelivery = await _dbContext.Deliveries
            .FirstOrDefaultAsync(d => d.Id == deliveryId && d.TenantId == tenantId, cancellationToken);

        if (initialDelivery is null)
        {
            return Result.Failure<DeliveryLineageResponse>(DomainError.NotFound(
                "Delivery.NotFound",
                $"Delivery with ID '{deliveryId}' was not found."));
        }

        // 1. Traverse upwards to find the root delivery
        var visitedIds = new HashSet<Guid> { initialDelivery.Id };
        var current = initialDelivery;

        while (current.OriginalDeliveryId.HasValue && current.OriginalDeliveryId.Value != Guid.Empty)
        {
            var parentId = current.OriginalDeliveryId.Value;
            if (visitedIds.Contains(parentId))
            {
                break; // Cycle safeguard
            }

            var parent = await _dbContext.Deliveries
                .FirstOrDefaultAsync(d => d.Id == parentId && d.TenantId == tenantId, cancellationToken);

            if (parent is null)
            {
                break;
            }

            visitedIds.Add(parent.Id);
            current = parent;
        }

        var rootDelivery = current;

        // 2. Query all deliveries in this tenant sharing the same EventId and EndpointId
        // or linked by OriginalDeliveryId
        var allRelatedDeliveries = await _dbContext.Deliveries
            .Where(d => d.TenantId == tenantId &&
                       (d.EventId == rootDelivery.EventId || visitedIds.Contains(d.Id) || (d.OriginalDeliveryId != null && visitedIds.Contains(d.OriginalDeliveryId.Value))))
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        var lineageDtos = allRelatedDeliveries.Select(d => new DeliveryResponse(
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
            d.UpdatedAt)).ToList();

        return Result.Success(new DeliveryLineageResponse(rootDelivery.Id, lineageDtos));
    }
}
