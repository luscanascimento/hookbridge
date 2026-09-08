using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Sandbox;

public sealed class ClearSandboxRequestsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ISandboxRealtimeNotifier? _realtimeNotifier;

    public ClearSandboxRequestsUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ISandboxRealtimeNotifier? realtimeNotifier = null)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<Result> ExecuteAsync(Guid sandboxId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure(DomainError.Unauthorized("Tenant.Unauthenticated", "Tenant context is required."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var sandboxExists = await _dbContext.WebhookSandboxes
            .AnyAsync(s => s.Id == sandboxId && s.TenantId == tenantId, cancellationToken);

        if (!sandboxExists)
        {
            return Result.Failure(DomainError.NotFound("WebhookSandbox.NotFound", $"Webhook sandbox '{sandboxId}' was not found."));
        }

        var requests = await _dbContext.SandboxRequests
            .Where(r => r.SandboxId == sandboxId && r.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (requests.Count > 0)
        {
            _dbContext.SandboxRequests.RemoveRange(requests);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (_realtimeNotifier != null)
            {
                await _realtimeNotifier.NotifyRequestsClearedAsync(tenantId, sandboxId, cancellationToken);
            }
        }

        return Result.Success();
    }
}
