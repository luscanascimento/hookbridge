using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;

namespace HookBridge.Domain.Entities;

public sealed class User : Entity<Guid>, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private User() { }

    public static Result<User> Create(Guid tenantId, string email, string passwordHash, UserRole role, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<User>(DomainError.Validation("User.InvalidTenantId", "TenantId cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return Result.Failure<User>(DomainError.Validation("User.InvalidEmail", "A valid email address is required."));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<User>(DomainError.Validation("User.EmptyPasswordHash", "Password hash cannot be empty."));
        }

        return Result.Success(new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = role,
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    public void RecordLogin(DateTimeOffset now)
    {
        LastLoginAt = now;
        UpdatedAt = now;
    }
}
