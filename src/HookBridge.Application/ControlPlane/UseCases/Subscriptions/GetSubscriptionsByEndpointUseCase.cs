using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Subscriptions;

public sealed class GetSubscriptionsByEndpointUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetSubscriptionsByEndpointUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<SubscriptionResponse>>> ExecuteAsync(Guid endpointId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<SubscriptionResponse>>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var endpointExists = await _dbContext.Endpoints
            .AnyAsync(e => e.Id == endpointId && e.TenantId == tenantId, cancellationToken);

        if (!endpointExists)
        {
            return Result.Failure<IReadOnlyList<SubscriptionResponse>>(DomainError.NotFound(
                "Endpoint.NotFound",
                $"Endpoint with ID '{endpointId}' was not found."));
        }

        var subscriptions = await _dbContext.Subscriptions
            .AsNoTracking()
            .Where(s => s.EndpointId == endpointId && s.TenantId == tenantId)
            .OrderBy(s => s.EventTypePattern)
            .Select(s => new SubscriptionResponse(
                s.Id,
                s.TenantId,
                s.EndpointId,
                s.EventTypePattern,
                s.IsActive,
                s.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SubscriptionResponse>>(subscriptions);
    }
}
