namespace HookBridge.Application.ControlPlane.DTOs;

public sealed record CreateSubscriptionCommand(
    string EventTypePattern);

public sealed record SubscriptionResponse(
    Guid Id,
    Guid TenantId,
    Guid EndpointId,
    string EventTypePattern,
    bool IsActive,
    DateTimeOffset CreatedAt);
