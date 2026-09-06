using HookBridge.Domain.Common;

namespace HookBridge.Domain.Entities;

public sealed class EventSchemaVersion : Entity<Guid>, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    public Guid EventSchemaId { get; private set; }
    public EventSchema? EventSchema { get; private set; }
    public string Version { get; private set; } = string.Empty;
    public int VersionNumber { get; private set; }
    public string SchemaJson { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? SamplePayloadJson { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeprecated { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private EventSchemaVersion() { }

    public static Result<EventSchemaVersion> Create(
        Guid tenantId,
        Guid eventSchemaId,
        string version,
        int versionNumber,
        string schemaJson,
        string? description,
        string? samplePayloadJson,
        bool isActive,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<EventSchemaVersion>(DomainError.Validation("EventSchemaVersion.InvalidTenantId", "TenantId cannot be empty."));
        }

        if (eventSchemaId == Guid.Empty)
        {
            return Result.Failure<EventSchemaVersion>(DomainError.Validation("EventSchemaVersion.InvalidEventSchemaId", "EventSchemaId cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return Result.Failure<EventSchemaVersion>(DomainError.Validation("EventSchemaVersion.EmptyVersion", "Version cannot be empty."));
        }

        if (versionNumber <= 0)
        {
            return Result.Failure<EventSchemaVersion>(DomainError.Validation("EventSchemaVersion.InvalidVersionNumber", "VersionNumber must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return Result.Failure<EventSchemaVersion>(DomainError.Validation("EventSchemaVersion.EmptySchemaJson", "Schema JSON cannot be empty."));
        }

        return Result.Success(new EventSchemaVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventSchemaId = eventSchemaId,
            Version = version.Trim(),
            VersionNumber = versionNumber,
            SchemaJson = schemaJson.Trim(),
            Description = description?.Trim(),
            SamplePayloadJson = samplePayloadJson?.Trim(),
            IsActive = isActive,
            IsDeprecated = false,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        IsDeprecated = false;
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    public void Deprecate(DateTimeOffset now)
    {
        IsDeprecated = true;
        UpdatedAt = now;
    }

    public void UpdateSamplePayload(string? samplePayloadJson, DateTimeOffset now)
    {
        SamplePayloadJson = samplePayloadJson?.Trim();
        UpdatedAt = now;
    }
}
