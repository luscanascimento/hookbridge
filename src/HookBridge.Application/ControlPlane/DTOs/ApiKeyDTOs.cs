using HookBridge.Domain.Enums;

namespace HookBridge.Application.ControlPlane.DTOs;

public sealed record CreateApiKeyCommand(
    string Name,
    ApiKeyScope Scopes,
    string Environment = "live",
    DateTimeOffset? ExpiresAt = null);

public sealed record ApiKeyCreatedResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Key,
    string KeyPrefix,
    ApiKeyScope Scopes,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);

public sealed record ApiKeyResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string KeyPrefix,
    ApiKeyScope Scopes,
    bool IsActive,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset CreatedAt);
