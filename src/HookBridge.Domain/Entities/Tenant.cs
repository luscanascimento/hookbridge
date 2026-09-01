using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;

namespace HookBridge.Domain.Entities;

public sealed class Tenant : AggregateRoot<Guid>, IAuditableEntity
{
    public string Identifier { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Navigation collections for EF Core
    public ICollection<User> Users { get; private set; } = new List<User>();
    public ICollection<Application> Applications { get; private set; } = new List<Application>();
    public ICollection<ApiKey> ApiKeys { get; private set; } = new List<ApiKey>();

    private Tenant() { }

    public static Result<Tenant> Create(string identifier, string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result.Failure<Tenant>(DomainError.Validation("Tenant.EmptyIdentifier", "Tenant identifier cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Tenant>(DomainError.Validation("Tenant.EmptyName", "Tenant name cannot be empty."));
        }

        return Result.Success(new Tenant
        {
            Id = Guid.NewGuid(),
            Identifier = identifier.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Status = TenantStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    public void Update(string name, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        UpdatedAt = now;
    }

    public void SetStatus(TenantStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
    }
}
