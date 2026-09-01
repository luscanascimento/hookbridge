using HookBridge.Domain.Common;

namespace HookBridge.Domain.Entities;

public sealed class Subscription : Entity<Guid>, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    public Guid EndpointId { get; private set; }
    public string EventTypePattern { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private Subscription() { }

    public static Result<Subscription> Create(Guid tenantId, Guid endpointId, string eventTypePattern, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || endpointId == Guid.Empty)
        {
            return Result.Failure<Subscription>(DomainError.Validation("Subscription.InvalidScope", "TenantId and EndpointId are required."));
        }

        if (string.IsNullOrWhiteSpace(eventTypePattern))
        {
            return Result.Failure<Subscription>(DomainError.Validation("Subscription.EmptyPattern", "EventTypePattern cannot be empty."));
        }

        return Result.Success(new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EndpointId = endpointId,
            EventTypePattern = eventTypePattern.Trim().ToLowerInvariant(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
    }
}
