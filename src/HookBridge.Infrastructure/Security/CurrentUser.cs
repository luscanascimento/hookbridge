using HookBridge.Application.Abstractions;
using HookBridge.Domain.Enums;

namespace HookBridge.Infrastructure.Security;

public sealed class CurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public UserRole? Role { get; set; }
    public bool IsAuthenticated => UserId.HasValue;
}
