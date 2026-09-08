using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed class ClearSimulatorExecutionsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ISimulatorRealtimeNotifier? _realtimeNotifier;

    public ClearSimulatorExecutionsUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ISimulatorRealtimeNotifier? realtimeNotifier = null)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<Result> ExecuteAsync(Guid? ruleId = null, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure(DomainError.Unauthorized("Tenant.Unauthenticated", "Tenant context is required."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var query = _dbContext.SimulatorExecutions.AsQueryable();

        if (ruleId.HasValue)
        {
            query = query.Where(e => e.RuleId == ruleId.Value);
        }

        var executions = await query.ToListAsync(cancellationToken);
        _dbContext.SimulatorExecutions.RemoveRange(executions);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_realtimeNotifier != null)
        {
            await _realtimeNotifier.NotifyExecutionsClearedAsync(tenantId, ruleId, cancellationToken);
        }

        return Result.Success();
    }
}
