using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.WebhookSecrets;

public sealed class GetEndpointSecretsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetEndpointSecretsUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<WebhookSecretResponse>>> ExecuteAsync(Guid endpointId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<WebhookSecretResponse>>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var endpointExists = await _dbContext.Endpoints
            .AnyAsync(e => e.Id == endpointId && e.TenantId == tenantId, cancellationToken);

        if (!endpointExists)
        {
            return Result.Failure<IReadOnlyList<WebhookSecretResponse>>(DomainError.NotFound(
                "Endpoint.NotFound",
                $"Endpoint with ID '{endpointId}' was not found."));
        }

        var secrets = await _dbContext.WebhookSecrets
            .AsNoTracking()
            .Where(s => s.EndpointId == endpointId && s.TenantId == tenantId)
            .OrderByDescending(s => s.Version)
            .Select(s => new WebhookSecretResponse(
                s.Id,
                s.EndpointId,
                s.KeyPrefix,
                s.Version,
                s.Status,
                s.CreatedAt,
                s.RevokedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<WebhookSecretResponse>>(secrets);
    }
}
