using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;

namespace HookBridge.Domain.Entities;

public sealed class ApiKey : AggregateRoot<Guid>, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string KeyPrefix { get; private set; } = string.Empty;
    public string KeyHash { get; private set; } = string.Empty;
    public ApiKeyScope Scopes { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public bool IsActive => RevokedAt is null && (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow);

    private ApiKey() { }

    public static Result<ApiKey> Create(
        Guid tenantId,
        string name,
        string keyPrefix,
        string keyHash,
        ApiKeyScope scopes,
        DateTimeOffset now,
        DateTimeOffset? expiresAt = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<ApiKey>(DomainError.Validation("ApiKey.InvalidTenantId", "TenantId cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<ApiKey>(DomainError.Validation("ApiKey.EmptyName", "ApiKey name is required."));
        }

        return Result.Success(new ApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            KeyPrefix = keyPrefix,
            KeyHash = keyHash,
            Scopes = scopes,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    public void Revoke(DateTimeOffset now)
    {
        RevokedAt = now;
        UpdatedAt = now;
    }
}
