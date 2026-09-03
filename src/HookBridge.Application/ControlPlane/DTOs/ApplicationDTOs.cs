namespace HookBridge.Application.ControlPlane.DTOs;

public sealed record CreateApplicationCommand(
    string Name,
    string? Description);

public sealed record UpdateApplicationCommand(
    string Name,
    string? Description,
    bool IsActive = true);

public sealed record ApplicationResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    bool IsActive,
    int EndpointsCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record ApplicationSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int EndpointsCount,
    DateTimeOffset CreatedAt);
