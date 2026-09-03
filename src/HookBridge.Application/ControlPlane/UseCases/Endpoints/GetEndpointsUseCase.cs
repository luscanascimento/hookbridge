using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Endpoints;

public sealed class GetEndpointsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetEndpointsUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<EndpointResponse>>> ExecuteAsync(Guid? applicationId = null, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<EndpointResponse>>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var query = _dbContext.Endpoints
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId);

        if (applicationId.HasValue && applicationId.Value != Guid.Empty)
        {
            query = query.Where(e => e.ApplicationId == applicationId.Value);
        }

        var endpoints = await query
            .Include(e => e.Secrets)
            .Include(e => e.Subscriptions)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var responses = endpoints.Select(e =>
        {
            var activeSecret = e.Secrets.FirstOrDefault(s => s.Status == SecretStatus.Active)
                ?? e.Secrets.OrderByDescending(s => s.Version).FirstOrDefault();

            var events = e.Subscriptions
                .Where(s => s.IsActive)
                .Select(s => s.EventTypePattern)
                .ToList();

            return new EndpointResponse(
                e.Id,
                e.TenantId,
                e.ApplicationId,
                e.TargetUrl,
                e.Description,
                e.Status,
                e.DisabledReason,
                e.RateLimitPerMinute,
                e.TimeoutSeconds,
                activeSecret?.KeyPrefix,
                activeSecret?.Version ?? 1,
                events,
                e.CreatedAt,
                e.UpdatedAt);
        }).ToList();

        return Result.Success<IReadOnlyList<EndpointResponse>>(responses);
    }
}
