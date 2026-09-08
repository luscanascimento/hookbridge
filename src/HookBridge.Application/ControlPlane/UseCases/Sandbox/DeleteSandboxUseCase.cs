using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Sandbox;

public sealed class DeleteSandboxUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public DeleteSandboxUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result> ExecuteAsync(Guid sandboxId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure(DomainError.Unauthorized("Tenant.Unauthenticated", "Tenant context is required."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var sandbox = await _dbContext.WebhookSandboxes
            .FirstOrDefaultAsync(s => s.Id == sandboxId && s.TenantId == tenantId, cancellationToken);

        if (sandbox == null)
        {
            return Result.Failure(DomainError.NotFound("WebhookSandbox.NotFound", $"Webhook sandbox '{sandboxId}' was not found."));
        }

        _dbContext.WebhookSandboxes.Remove(sandbox);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
