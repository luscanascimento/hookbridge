using HookBridge.Domain.Enums;

namespace HookBridge.Application.ControlPlane.DTOs;

/// <summary>
/// Represents a real-time event dispatched to SignalR clients for live delivery monitoring.
/// </summary>
public sealed record RealtimeDeliveryEvent(
    string EventType,
    Guid DeliveryId,
    Guid TenantId,
    Guid EndpointId,
    string EventName,
    DeliveryStatus Status,
    int AttemptCount,
    string CorrelationId,
    string? TraceParent,
    DateTimeOffset Timestamp,
    AttemptResponse? Attempt = null,
    Guid? OriginalDeliveryId = null,
    string? EndpointUrl = null);
