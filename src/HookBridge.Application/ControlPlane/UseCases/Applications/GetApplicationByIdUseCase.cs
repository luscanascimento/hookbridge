using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Applications;

public sealed class GetApplicationByIdUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetApplicationByIdUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<ApplicationResponse>> ExecuteAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<ApplicationResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var app = await _dbContext.Applications
            .AsNoTracking()
            .Where(a => a.Id == applicationId && a.TenantId == tenantId)
            .Select(a => new ApplicationResponse(
                a.Id,
                a.TenantId,
                a.Name,
                a.Description,
                a.IsActive,
                a.Endpoints.Count,
                a.CreatedAt,
                a.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (app is null)
        {
            return Result.Failure<ApplicationResponse>(DomainError.NotFound(
                "Application.NotFound",
                $"Application with ID '{applicationId}' was not found."));
        }

        return Result.Success(app);
    }
}
