using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Diagnostics;
using HookBridge.Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace HookBridge.Api.Hubs;

/// <summary>
/// Dispatches real-time webhook delivery updates to connected SignalR clients scoped by tenant and endpoint groups.
/// </summary>
public sealed partial class DeliveryRealtimeNotifier : IDeliveryRealtimeNotifier
{
    private readonly IHubContext<DeliveryHub, IDeliveryHubClient> _hubContext;
    private readonly ILogger<DeliveryRealtimeNotifier> _logger;

    public DeliveryRealtimeNotifier(
        IHubContext<DeliveryHub, IDeliveryHubClient> hubContext,
        ILogger<DeliveryRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Debug, Message = "Broadcasting DeliveryDispatched event for delivery {DeliveryId} to {TenantGroup}.")]
    private static partial void LogDispatched(ILogger logger, Guid deliveryId, string tenantGroup);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Debug, Message = "Broadcasting DeliveryAttemptRecorded event for delivery {DeliveryId} (attempt {AttemptNumber}) to {TenantGroup}.")]
    private static partial void LogAttemptRecorded(ILogger logger, Guid deliveryId, int attemptNumber, string tenantGroup);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Debug, Message = "Broadcasting DeliveryReplayed event for new delivery {DeliveryId} (replaying {OriginalDeliveryId}) to {TenantGroup}.")]
    private static partial void LogReplayed(ILogger logger, Guid deliveryId, Guid originalDeliveryId, string tenantGroup);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Debug, Message = "Broadcasting bulk replay of {Count} deliveries to {TenantGroup}.")]
    private static partial void LogBulkReplayed(ILogger logger, int count, string tenantGroup);

    public async Task NotifyDeliveryDispatchedAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        var evt = new RealtimeDeliveryEvent(
            EventType: "DeliveryDispatched",
            DeliveryId: delivery.Id,
            TenantId: delivery.TenantId,
            EndpointId: delivery.EndpointId,
            EventName: delivery.EventType,
            Status: delivery.Status,
            AttemptCount: delivery.AttemptCount,
            CorrelationId: delivery.CorrelationId,
            TraceParent: delivery.TraceParent,
            Timestamp: delivery.ScheduledAt,
            Attempt: null,
            OriginalDeliveryId: delivery.OriginalDeliveryId);

        var tenantGroup = DeliveryHub.GetTenantGroup(delivery.TenantId);
        var endpointGroup = DeliveryHub.GetEndpointGroup(delivery.TenantId, delivery.EndpointId);

        LogDispatched(_logger, delivery.Id, tenantGroup);

        await _hubContext.Clients.Group(tenantGroup).ReceiveDeliveryEvent(evt);
        await _hubContext.Clients.Group(tenantGroup).DeliveryDispatched(evt);

        await _hubContext.Clients.Group(endpointGroup).ReceiveDeliveryEvent(evt);
        await _hubContext.Clients.Group(endpointGroup).DeliveryDispatched(evt);

        HookBridgeDiagnostics.RealtimeEventsBroadcasted.Add(1,
            new KeyValuePair<string, object?>("tenant.id", delivery.TenantId.ToString()),
            new KeyValuePair<string, object?>("event.type", "DeliveryDispatched"));
    }

    public async Task NotifyDeliveryAttemptRecordedAsync(Delivery delivery, Attempt attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(attempt);

        var attemptDto = new AttemptResponse(
            attempt.Id,
            attempt.DeliveryId,
            attempt.AttemptNumber,
            attempt.HttpStatusCode,
            attempt.RequestHeadersJson,
            attempt.RequestBody,
            attempt.ResponseHeadersJson,
            attempt.ResponseBody,
            attempt.ElapsedMs,
            attempt.ErrorMessage,
            attempt.ExecutedAt);

        var evt = new RealtimeDeliveryEvent(
            EventType: "DeliveryAttemptRecorded",
            DeliveryId: delivery.Id,
            TenantId: delivery.TenantId,
            EndpointId: delivery.EndpointId,
            EventName: delivery.EventType,
            Status: delivery.Status,
            AttemptCount: delivery.AttemptCount,
            CorrelationId: delivery.CorrelationId,
            TraceParent: delivery.TraceParent,
            Timestamp: attempt.ExecutedAt,
            Attempt: attemptDto,
            OriginalDeliveryId: delivery.OriginalDeliveryId);

        var tenantGroup = DeliveryHub.GetTenantGroup(delivery.TenantId);
        var endpointGroup = DeliveryHub.GetEndpointGroup(delivery.TenantId, delivery.EndpointId);

        LogAttemptRecorded(_logger, delivery.Id, attempt.AttemptNumber, tenantGroup);

        await _hubContext.Clients.Group(tenantGroup).ReceiveDeliveryEvent(evt);
        await _hubContext.Clients.Group(tenantGroup).DeliveryAttemptRecorded(evt);

        await _hubContext.Clients.Group(endpointGroup).ReceiveDeliveryEvent(evt);
        await _hubContext.Clients.Group(endpointGroup).DeliveryAttemptRecorded(evt);

        HookBridgeDiagnostics.RealtimeEventsBroadcasted.Add(1,
            new KeyValuePair<string, object?>("tenant.id", delivery.TenantId.ToString()),
            new KeyValuePair<string, object?>("event.type", "DeliveryAttemptRecorded"));
    }

    public async Task NotifyDeliveryReplayedAsync(Delivery delivery, Guid originalDeliveryId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        var evt = new RealtimeDeliveryEvent(
            EventType: "DeliveryReplayed",
            DeliveryId: delivery.Id,
            TenantId: delivery.TenantId,
            EndpointId: delivery.EndpointId,
            EventName: delivery.EventType,
            Status: delivery.Status,
            AttemptCount: delivery.AttemptCount,
            CorrelationId: delivery.CorrelationId,
            TraceParent: delivery.TraceParent,
            Timestamp: delivery.ScheduledAt,
            Attempt: null,
            OriginalDeliveryId: originalDeliveryId);

        var tenantGroup = DeliveryHub.GetTenantGroup(delivery.TenantId);
        var endpointGroup = DeliveryHub.GetEndpointGroup(delivery.TenantId, delivery.EndpointId);

        LogReplayed(_logger, delivery.Id, originalDeliveryId, tenantGroup);

        await _hubContext.Clients.Group(tenantGroup).ReceiveDeliveryEvent(evt);
        await _hubContext.Clients.Group(tenantGroup).DeliveryReplayed(evt);

        await _hubContext.Clients.Group(endpointGroup).ReceiveDeliveryEvent(evt);
        await _hubContext.Clients.Group(endpointGroup).DeliveryReplayed(evt);

        HookBridgeDiagnostics.RealtimeEventsBroadcasted.Add(1,
            new KeyValuePair<string, object?>("tenant.id", delivery.TenantId.ToString()),
            new KeyValuePair<string, object?>("event.type", "DeliveryReplayed"));
    }

    public async Task NotifyBulkDeliveriesReplayedAsync(Guid tenantId, IReadOnlyList<Delivery> replayedDeliveries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replayedDeliveries);

        if (replayedDeliveries.Count == 0)
        {
            return;
        }

        var events = replayedDeliveries.Select(d => new RealtimeDeliveryEvent(
            EventType: "DeliveryReplayed",
            DeliveryId: d.Id,
            TenantId: d.TenantId,
            EndpointId: d.EndpointId,
            EventName: d.EventType,
            Status: d.Status,
            AttemptCount: d.AttemptCount,
            CorrelationId: d.CorrelationId,
            TraceParent: d.TraceParent,
            Timestamp: d.ScheduledAt,
            Attempt: null,
            OriginalDeliveryId: d.OriginalDeliveryId)).ToList();

        var tenantGroup = DeliveryHub.GetTenantGroup(tenantId);

        LogBulkReplayed(_logger, events.Count, tenantGroup);

        await _hubContext.Clients.Group(tenantGroup).BulkDeliveriesReplayed(events);

        foreach (var evt in events)
        {
            await _hubContext.Clients.Group(tenantGroup).ReceiveDeliveryEvent(evt);

            var endpointGroup = DeliveryHub.GetEndpointGroup(tenantId, evt.EndpointId);
            await _hubContext.Clients.Group(endpointGroup).ReceiveDeliveryEvent(evt);
            await _hubContext.Clients.Group(endpointGroup).DeliveryReplayed(evt);
        }

        HookBridgeDiagnostics.RealtimeEventsBroadcasted.Add(events.Count,
            new KeyValuePair<string, object?>("tenant.id", tenantId.ToString()),
            new KeyValuePair<string, object?>("event.type", "DeliveryReplayed"));
    }
}
