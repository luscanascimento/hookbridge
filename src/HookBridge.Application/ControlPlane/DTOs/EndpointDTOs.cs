using HookBridge.Domain.Enums;

namespace HookBridge.Application.ControlPlane.DTOs;

public sealed record CreateEndpointCommand(
    Guid ApplicationId,
    string TargetUrl,
    string? Description = null,
    int RateLimitPerMinute = 600,
    int TimeoutSeconds = 10,
    List<string>? SubscribedEvents = null);

public sealed record UpdateEndpointCommand(
    string TargetUrl,
    string? Description,
    int RateLimitPerMinute = 600,
    int TimeoutSeconds = 10);

public sealed record UpdateEndpointStatusCommand(
    EndpointStatus Status,
    string? Reason = null);

public sealed record EndpointResponse(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    string TargetUrl,
    string? Description,
    EndpointStatus Status,
    string? DisabledReason,
    int RateLimitPerMinute,
    int TimeoutSeconds,
    string? ActiveSecretPrefix,
    int ActiveSecretVersion,
    IReadOnlyList<string> SubscribedEvents,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record EndpointCreatedResponse(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    string TargetUrl,
    string? Description,
    EndpointStatus Status,
    int RateLimitPerMinute,
    int TimeoutSeconds,
    string InitialSecret,
    string SecretPrefix,
    int SecretVersion,
    IReadOnlyList<string> SubscribedEvents,
    DateTimeOffset CreatedAt);
