using System.Diagnostics;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Observability;

public sealed class GetObservabilitySummaryUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly IObservabilityOptions _observabilityOptions;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetObservabilitySummaryUseCase(
        IHookBridgeDbContext dbContext,
        IObservabilityOptions observabilityOptions,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _observabilityOptions = observabilityOptions;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ObservabilitySummaryResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var process = Process.GetCurrentProcess();

        var totalDeliveries = await _dbContext.Deliveries.CountAsync(cancellationToken);
        var succeededDeliveries = await _dbContext.Deliveries.CountAsync(d => d.Status == Domain.Enums.DeliveryStatus.Success, cancellationToken);
        var failedDeliveries = await _dbContext.Deliveries.CountAsync(d => d.Status == Domain.Enums.DeliveryStatus.Failed, cancellationToken);
        var deadLetteredDeliveries = await _dbContext.Deliveries.CountAsync(d => d.Status == Domain.Enums.DeliveryStatus.DeadLettered, cancellationToken);
        var totalAttempts = await _dbContext.Attempts.CountAsync(cancellationToken);

        var totalReplays = await _dbContext.AuditEntries.CountAsync(a => a.Action.Contains("Replay"), cancellationToken);
        var simulatorExecs = await _dbContext.SimulatorExecutions.CountAsync(cancellationToken);
        var simulatorFailures = await _dbContext.SimulatorExecutions.CountAsync(s => s.SimulatedStatusCode >= 400, cancellationToken);
        var sandboxRequests = await _dbContext.SandboxRequests.CountAsync(cancellationToken);
        var schemaCount = await _dbContext.EventSchemas.CountAsync(cancellationToken);
        var endpointsCount = await _dbContext.Endpoints.CountAsync(cancellationToken);

        var isOtlpConfigured = !string.IsNullOrWhiteSpace(_observabilityOptions.OtlpEndpoint);

        var counters = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["hookbridge.deliveries.total"] = totalDeliveries,
            ["hookbridge.deliveries.succeeded"] = succeededDeliveries,
            ["hookbridge.deliveries.failed"] = failedDeliveries,
            ["hookbridge.deliveries.deadlettered"] = deadLetteredDeliveries,
            ["hookbridge.attempts.total"] = totalAttempts,
            ["hookbridge.replays.total"] = totalReplays,
            ["hookbridge.simulator.executions"] = simulatorExecs,
            ["hookbridge.simulator.failures"] = simulatorFailures,
            ["hookbridge.sandbox.requests"] = sandboxRequests,
            ["hookbridge.schemas.total"] = schemaCount,
            ["hookbridge.endpoints.total"] = endpointsCount
        };

        var workingSetMb = Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 2);
        var heapMb = Math.Round(GC.GetTotalMemory(false) / (1024.0 * 1024.0), 2);

        return Result.Success(new ObservabilitySummaryResponse(
            ServiceName: HookBridgeDiagnostics.ServiceName,
            ServiceVersion: HookBridgeDiagnostics.Version,
            Environment: _observabilityOptions.EnvironmentName,
            OtlpExporterConfigured: isOtlpConfigured,
            OtlpEndpoint: _observabilityOptions.OtlpEndpoint,
            RegisteredInstrumentsCount: 16,
            TotalRecordedSpansCount: totalAttempts + simulatorExecs + sandboxRequests,
            ProcessWorkingSetMb: workingSetMb,
            ProcessHeapMb: heapMb,
            GcGen0Collections: GC.CollectionCount(0),
            GcGen1Collections: GC.CollectionCount(1),
            GcGen2Collections: GC.CollectionCount(2),
            MetricCounters: counters,
            Timestamp: now
        ));
    }
}
