using HookBridge.Domain.Entities;

namespace HookBridge.Application.Abstractions;

public interface ISimulatorRealtimeNotifier
{
    Task NotifyExecutionCapturedAsync(SimulatorRule? rule, SimulatorExecution execution, CancellationToken cancellationToken = default);
    Task NotifyExecutionsClearedAsync(Guid tenantId, Guid? ruleId, CancellationToken cancellationToken = default);
}
