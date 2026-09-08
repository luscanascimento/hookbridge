using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.UseCases.Simulator;
using HookBridge.Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace HookBridge.Api.Hubs;

public sealed partial class SimulatorRealtimeNotifier : ISimulatorRealtimeNotifier
{
    private readonly IHubContext<DeliveryHub, IDeliveryHubClient> _hubContext;
    private readonly ILogger<SimulatorRealtimeNotifier> _logger;

    public SimulatorRealtimeNotifier(
        IHubContext<DeliveryHub, IDeliveryHubClient> hubContext,
        ILogger<SimulatorRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    [LoggerMessage(EventId = 3020, Level = LogLevel.Information, Message = "Broadcasting SimulatorExecutionCaptured for rule {RuleSlug} to {TenantGroup}.")]
    private static partial void LogExecutionCaptured(ILogger logger, string ruleSlug, string tenantGroup);

    [LoggerMessage(EventId = 3021, Level = LogLevel.Information, Message = "Broadcasting SimulatorExecutionsCleared for rule {RuleId} to {TenantGroup}.")]
    private static partial void LogExecutionsCleared(ILogger logger, Guid? ruleId, string tenantGroup);

    public async Task NotifyExecutionCapturedAsync(SimulatorRule? rule, SimulatorExecution execution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);

        var executionDto = new SimulatorExecutionResponse(
            execution.Id,
            execution.RuleId,
            rule?.Name ?? (execution.RuleId.HasValue ? "Simulator Rule" : "Ad-Hoc Simulation"),
            execution.HttpMethod,
            execution.Path,
            execution.QueryString,
            execution.HeadersJson,
            execution.Body,
            execution.ContentType,
            execution.ContentLength,
            execution.ClientIp,
            execution.InjectedFault,
            execution.SimulatedStatusCode,
            execution.SimulatedDelayMs,
            execution.SimulatedHeadersJson,
            execution.SimulatedResponseBody,
            execution.ExecutionDurationMs,
            execution.ExecutedAt
        );

        var evt = new RealtimeSimulatorEvent(
            EventType: "SimulatorExecutionCaptured",
            TenantId: execution.TenantId,
            RuleId: execution.RuleId,
            RuleSlug: rule?.Slug,
            Execution: executionDto,
            Timestamp: execution.ExecutedAt
        );

        var tenantGroup = DeliveryHub.GetTenantGroup(execution.TenantId);

        LogExecutionCaptured(_logger, rule?.Slug ?? "ad-hoc", tenantGroup);

        await _hubContext.Clients.Group(tenantGroup).ReceiveSimulatorExecution(evt);
    }

    public async Task NotifyExecutionsClearedAsync(Guid tenantId, Guid? ruleId, CancellationToken cancellationToken = default)
    {
        var tenantGroup = DeliveryHub.GetTenantGroup(tenantId);
        LogExecutionsCleared(_logger, ruleId, tenantGroup);

        await _hubContext.Clients.Group(tenantGroup).SimulatorExecutionsCleared(ruleId);
    }
}
