using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Deliveries;

public sealed class GetDeliveryByIdUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetDeliveryByIdUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<DeliveryDetailResponse>> ExecuteAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<DeliveryDetailResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var delivery = await _dbContext.Deliveries
            .Include(d => d.Attempts)
            .FirstOrDefaultAsync(d => d.Id == deliveryId && d.TenantId == tenantId, cancellationToken);

        if (delivery is null)
        {
            return Result.Failure<DeliveryDetailResponse>(DomainError.NotFound(
                "Delivery.NotFound",
                $"Delivery with ID '{deliveryId}' was not found."));
        }

        var attempts = delivery.Attempts
            .OrderBy(a => a.AttemptNumber)
            .Select(a => new AttemptResponse(
                a.Id,
                a.DeliveryId,
                a.AttemptNumber,
                a.HttpStatusCode,
                a.RequestHeadersJson,
                a.RequestBody,
                a.ResponseHeadersJson,
                a.ResponseBody,
                a.ElapsedMs,
                a.ErrorMessage,
                a.ExecutedAt))
            .ToList();

        var endpoint = await _dbContext.Endpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == delivery.EndpointId && e.TenantId == tenantId, cancellationToken);

        return Result.Success(new DeliveryDetailResponse(
            delivery.Id,
            delivery.TenantId,
            delivery.EventId,
            delivery.EndpointId,
            delivery.SubscriptionId,
            delivery.EventType,
            delivery.Status,
            delivery.ScheduledAt,
            delivery.DeliveredAt,
            delivery.AttemptCount,
            delivery.TraceParent,
            delivery.CorrelationId,
            delivery.OriginalDeliveryId,
            delivery.CreatedAt,
            delivery.UpdatedAt,
            attempts,
            endpoint?.TargetUrl));
    }
}
