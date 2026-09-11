using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.Services;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Observability;

public sealed class GetPrometheusMetricsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;

    public GetPrometheusMetricsUseCase(IHookBridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var totalDeliveries = await _dbContext.Deliveries.IgnoreQueryFilters().CountAsync(cancellationToken);
        var succeededDeliveries = await _dbContext.Deliveries.IgnoreQueryFilters().CountAsync(d => d.Status == Domain.Enums.DeliveryStatus.Success, cancellationToken);
        var failedDeliveries = await _dbContext.Deliveries.IgnoreQueryFilters().CountAsync(d => d.Status == Domain.Enums.DeliveryStatus.Failed, cancellationToken);
        var deadLetteredDeliveries = await _dbContext.Deliveries.IgnoreQueryFilters().CountAsync(d => d.Status == Domain.Enums.DeliveryStatus.DeadLettered, cancellationToken);
        var replays = await _dbContext.AuditEntries.IgnoreQueryFilters().CountAsync(a => a.Action.Contains("Replay"), cancellationToken);

        var simulatorExecs = await _dbContext.SimulatorExecutions.IgnoreQueryFilters().CountAsync(cancellationToken);
        var simulatorFailures = await _dbContext.SimulatorExecutions.IgnoreQueryFilters().CountAsync(s => s.SimulatedStatusCode >= 400, cancellationToken);
        var sandboxRequests = await _dbContext.SandboxRequests.IgnoreQueryFilters().CountAsync(cancellationToken);
        var schemas = await _dbContext.EventSchemas.IgnoreQueryFilters().CountAsync(cancellationToken);
        var disabledEndpoints = await _dbContext.Endpoints.IgnoreQueryFilters().CountAsync(e => e.Status == Domain.Enums.EndpointStatus.Disabled, cancellationToken);

        return PrometheusMetricsFormatter.FormatMetrics(
            deliveriesDispatched: totalDeliveries,
            deliveriesSucceeded: succeededDeliveries,
            deliveriesFailed: failedDeliveries,
            deliveriesDeadLettered: deadLetteredDeliveries,
            replaysTriggered: replays,
            eventsPublished: totalDeliveries,
            signaturesGenerated: totalDeliveries,
            signaturesVerified: 0,
            simulatorExecutions: simulatorExecs,
            simulatorFailuresInjected: simulatorFailures,
            sandboxRequestsCaptured: sandboxRequests,
            schemaValidations: totalDeliveries,
            schemaDriftDetected: 0,
            activeSignalRConnections: 1,
            realtimeEventsBroadcasted: totalDeliveries,
            activeCircuitBreakers: disabledEndpoints
        );
    }
}
