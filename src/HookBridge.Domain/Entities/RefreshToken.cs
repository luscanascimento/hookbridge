using HookBridge.Domain.Common;

namespace HookBridge.Domain.Entities;

public sealed class RefreshToken : Entity<Guid>, ITenantScoped
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    private RefreshToken() { }

    public static Result<RefreshToken> Create(
        Guid userId,
        Guid tenantId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
        {
            return Result.Failure<RefreshToken>(DomainError.Validation("RefreshToken.InvalidScope", "UserId and TenantId are required."));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return Result.Failure<RefreshToken>(DomainError.Validation("RefreshToken.EmptyHash", "Token hash cannot be empty."));
        }

        return Result.Success(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime)
        });
    }

    public void Revoke(DateTimeOffset now, string? replacedByTokenHash = null)
    {
        RevokedAt = now;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
