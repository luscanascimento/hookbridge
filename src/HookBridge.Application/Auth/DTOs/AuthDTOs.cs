using HookBridge.Domain.Enums;

namespace HookBridge.Application.Auth.DTOs;

public sealed record RegisterTenantCommand(
    string TenantIdentifier,
    string TenantName,
    string AdminEmail,
    string AdminPassword);

public sealed record LoginCommand(
    string Email,
    string Password,
    string? TenantIdentifier);

public sealed record RefreshTokenCommand(
    string RefreshToken);

public sealed record InviteUserCommand(
    string Email,
    UserRole Role,
    string InitialPassword);

public sealed record UserProfileResponse(
    Guid UserId,
    Guid TenantId,
    string TenantIdentifier,
    string Email,
    UserRole Role,
    UserStatus Status,
    DateTimeOffset? LastLoginAt);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType,
    UserProfileResponse User);
