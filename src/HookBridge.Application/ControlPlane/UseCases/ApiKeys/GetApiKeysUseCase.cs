using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.ApiKeys;

public sealed class GetApiKeysUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetApiKeysUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<ApiKeyResponse>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<ApiKeyResponse>>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var keys = await _dbContext.ApiKeys
            .AsNoTracking()
            .Where(k => k.TenantId == tenantId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyResponse(
                k.Id,
                k.TenantId,
                k.Name,
                k.KeyPrefix,
                k.Scopes,
                k.IsActive,
                k.ExpiresAt,
                k.RevokedAt,
                k.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ApiKeyResponse>>(keys);
    }
}
