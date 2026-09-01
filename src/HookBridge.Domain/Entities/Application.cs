using HookBridge.Domain.Common;

namespace HookBridge.Domain.Entities;

public sealed class Application : AggregateRoot<Guid>, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public ICollection<Endpoint> Endpoints { get; private set; } = new List<Endpoint>();

    private Application() { }

    public static Result<Application> Create(Guid tenantId, string name, string? description, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Application>(DomainError.Validation("Application.InvalidTenantId", "TenantId cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Application>(DomainError.Validation("Application.EmptyName", "Application name is required."));
        }

        return Result.Success(new Application
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
    }
}
