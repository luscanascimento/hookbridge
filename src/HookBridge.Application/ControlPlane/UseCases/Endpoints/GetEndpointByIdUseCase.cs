using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Endpoints;

public sealed class GetEndpointByIdUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetEndpointByIdUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<EndpointResponse>> ExecuteAsync(Guid endpointId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<EndpointResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var endpoint = await _dbContext.Endpoints
            .AsNoTracking()
            .Include(e => e.Secrets)
            .Include(e => e.Subscriptions)
            .FirstOrDefaultAsync(e => e.Id == endpointId && e.TenantId == tenantId, cancellationToken);

        if (endpoint is null)
        {
            return Result.Failure<EndpointResponse>(DomainError.NotFound(
                "Endpoint.NotFound",
                $"Endpoint with ID '{endpointId}' was not found."));
        }

        var activeSecret = endpoint.Secrets.FirstOrDefault(s => s.Status == SecretStatus.Active)
            ?? endpoint.Secrets.OrderByDescending(s => s.Version).FirstOrDefault();

        var events = endpoint.Subscriptions
            .Where(s => s.IsActive)
            .Select(s => s.EventTypePattern)
            .ToList();

        return Result.Success(new EndpointResponse(
            endpoint.Id,
            endpoint.TenantId,
            endpoint.ApplicationId,
            endpoint.TargetUrl,
            endpoint.Description,
            endpoint.Status,
            endpoint.DisabledReason,
            endpoint.RateLimitPerMinute,
            endpoint.TimeoutSeconds,
            activeSecret?.KeyPrefix,
            activeSecret?.Version ?? 1,
            events,
            endpoint.CreatedAt,
            endpoint.UpdatedAt));
    }
}
