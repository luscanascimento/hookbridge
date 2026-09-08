using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Observability;

public sealed class GetMetricInstrumentsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;

    public GetMetricInstrumentsUseCase(IHookBridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<MetricInstrumentDto>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var totalDeliveries = await _dbContext.Deliveries.CountAsync(cancellationToken);
        var succeededDeliveries = await _dbContext.Deliveries.CountAsync(d => d.Status == Domain.Enums.DeliveryStatus.Success, cancellationToken);
        var failedDeliveries = await _dbContext.Deliveries.CountAsync(d => d.Status == Domain.Enums.DeliveryStatus.Failed, cancellationToken);
        var deadLettered = await _dbContext.Deliveries.CountAsync(d => d.Status == Domain.Enums.DeliveryStatus.DeadLettered, cancellationToken);
        var replays = await _dbContext.AuditEntries.CountAsync(a => a.Action.Contains("Replay"), cancellationToken);
        var simulatorExecs = await _dbContext.SimulatorExecutions.CountAsync(cancellationToken);
        var simulatorFailures = await _dbContext.SimulatorExecutions.CountAsync(s => s.SimulatedStatusCode >= 400, cancellationToken);
        var sandboxRequests = await _dbContext.SandboxRequests.CountAsync(cancellationToken);
        var schemas = await _dbContext.EventSchemas.CountAsync(cancellationToken);
        var disabledEndpoints = await _dbContext.Endpoints.CountAsync(e => e.Status == Domain.Enums.EndpointStatus.Disabled, cancellationToken);

        var instruments = new List<MetricInstrumentDto>
        {
            new("hookbridge.events.published", "{event}", "Total number of domain events published through ingestion gateway", "Counter", totalDeliveries),
            new("hookbridge.deliveries.dispatched", "{delivery}", "Total number of webhook deliveries dispatched to target endpoints", "Counter", totalDeliveries),
            new("hookbridge.deliveries.succeeded", "{delivery}", "Total number of webhook deliveries successfully acknowledged by targets", "Counter", succeededDeliveries),
            new("hookbridge.deliveries.failed", "{delivery}", "Total number of webhook deliveries that failed", "Counter", failedDeliveries),
            new("hookbridge.deliveries.deadlettered", "{delivery}", "Total number of webhook deliveries routed to the Dead Letter Queue (DLQ)", "Counter", deadLettered),
            new("hookbridge.replays.triggered", "{replay}", "Total number of manual or bulk delivery replays triggered", "Counter", replays),
            new("hookbridge.delivery.latency", "ms", "Duration of external webhook HTTP dispatch in milliseconds", "Histogram", 0),
            new("hookbridge.signatures.generated", "{signature}", "Total number of HMAC-SHA256 webhook signatures generated", "Counter", totalDeliveries),
            new("hookbridge.signatures.verified", "{verification}", "Total number of HMAC-SHA256 signature verifications evaluated", "Counter", 0),
            new("hookbridge.simulator.executions", "{execution}", "Total number of simulated webhook executions processed", "Counter", simulatorExecs),
            new("hookbridge.simulator.failures_injected", "{fault}", "Total number of controlled chaos faults injected by simulator", "Counter", simulatorFailures),
            new("hookbridge.sandbox.requests_captured", "{request}", "Total number of inbound requests captured by sandboxes", "Counter", sandboxRequests),
            new("hookbridge.schemas.registered", "{schema}", "Total number of event schemas registered", "Counter", schemas),
            new("hookbridge.schema.validations", "{validation}", "Total number of payload validations performed against schemas", "Counter", totalDeliveries),
            new("hookbridge.signalr.active_connections", "{connection}", "Current number of active authenticated SignalR live inspection connections", "UpDownCounter", 1),
            new("hookbridge.circuit_breakers.open", "{breaker}", "Current number of open circuit breakers across tenant endpoints", "UpDownCounter", disabledEndpoints)
        };

        return Result.Success<IReadOnlyList<MetricInstrumentDto>>(instruments);
    }
}
