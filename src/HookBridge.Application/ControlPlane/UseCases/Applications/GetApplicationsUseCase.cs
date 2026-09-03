using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Applications;

public sealed class GetApplicationsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetApplicationsUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<ApplicationSummaryResponse>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<ApplicationSummaryResponse>>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var apps = await _dbContext.Applications
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ApplicationSummaryResponse(
                a.Id,
                a.Name,
                a.Description,
                a.IsActive,
                a.Endpoints.Count,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ApplicationSummaryResponse>>(apps);
    }
}
