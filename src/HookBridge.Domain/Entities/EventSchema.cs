using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;

namespace HookBridge.Domain.Entities;

public sealed class EventSchema : AggregateRoot<Guid>, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public SchemaCompatibilityMode CompatibilityMode { get; private set; }
    public SchemaStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public ICollection<EventSchemaVersion> Versions { get; private set; } = new List<EventSchemaVersion>();

    private EventSchema() { }

    public static Result<EventSchema> Create(
        Guid tenantId,
        string eventType,
        string name,
        string? description,
        SchemaCompatibilityMode compatibilityMode,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<EventSchema>(DomainError.Validation("EventSchema.InvalidTenantId", "TenantId cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            return Result.Failure<EventSchema>(DomainError.Validation("EventSchema.EmptyEventType", "EventType cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<EventSchema>(DomainError.Validation("EventSchema.EmptyName", "Schema Name cannot be empty."));
        }

        return Result.Success(new EventSchema
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = eventType.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            CompatibilityMode = compatibilityMode,
            Status = SchemaStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    public void Update(string name, string? description, SchemaCompatibilityMode compatibilityMode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = description?.Trim();
        CompatibilityMode = compatibilityMode;
        UpdatedAt = now;
    }

    public void SetStatus(SchemaStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
    }
}
