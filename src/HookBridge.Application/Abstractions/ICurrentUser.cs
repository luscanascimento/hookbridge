using HookBridge.Domain.Enums;

namespace HookBridge.Application.Abstractions;

/// <summary>
/// Provides access to the authenticated user identity and claims for the current execution scope.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
}
