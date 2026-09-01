using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;

namespace HookBridge.Domain.Entities;

public sealed class WebhookSecret : Entity<Guid>, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    public Guid EndpointId { get; private set; }
    public string KeyPrefix { get; private set; } = string.Empty;
    public string SecretHash { get; private set; } = string.Empty;
    public string EncryptedSecret { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public SecretStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private WebhookSecret() { }

    public static Result<WebhookSecret> Create(
        Guid tenantId,
        Guid endpointId,
        string keyPrefix,
        string secretHash,
        string encryptedSecret,
        int version,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || endpointId == Guid.Empty)
        {
            return Result.Failure<WebhookSecret>(DomainError.Validation("WebhookSecret.InvalidScope", "TenantId and EndpointId are required."));
        }

        return Result.Success(new WebhookSecret
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EndpointId = endpointId,
            KeyPrefix = keyPrefix,
            SecretHash = secretHash,
            EncryptedSecret = encryptedSecret,
            Version = version,
            Status = SecretStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    public void MarkRotating(DateTimeOffset now)
    {
        Status = SecretStatus.Rotating;
        UpdatedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        Status = SecretStatus.Revoked;
        RevokedAt = now;
        UpdatedAt = now;
    }
}
