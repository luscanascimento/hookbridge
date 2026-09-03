using HookBridge.Domain.Enums;

namespace HookBridge.Application.ControlPlane.DTOs;

public sealed record WebhookSecretResponse(
    Guid Id,
    Guid EndpointId,
    string KeyPrefix,
    int Version,
    SecretStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt);

public sealed record RotateSecretResponse(
    Guid Id,
    Guid EndpointId,
    string NewSecret,
    string SecretPrefix,
    int Version,
    SecretStatus Status,
    DateTimeOffset CreatedAt);
