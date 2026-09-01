using HookBridge.Domain.Common;

namespace HookBridge.Domain.Entities;

public sealed class AuditEntry : Entity<Guid>, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public string ResourceId { get; private set; } = string.Empty;
    public string DetailsJson { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public string? TraceId { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }

    private AuditEntry() { }

    public static Result<AuditEntry> Create(
        Guid tenantId,
        Guid? userId,
        string action,
        string resourceType,
        string resourceId,
        string detailsJson,
        string? ipAddress,
        string? traceId,
        DateTimeOffset timestamp)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<AuditEntry>(DomainError.Validation("AuditEntry.InvalidTenantId", "TenantId cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            return Result.Failure<AuditEntry>(DomainError.Validation("AuditEntry.EmptyAction", "Action cannot be empty."));
        }

        return Result.Success(new AuditEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Action = action.Trim(),
            ResourceType = resourceType.Trim(),
            ResourceId = resourceId.Trim(),
            DetailsJson = detailsJson,
            IpAddress = ipAddress?.Trim(),
            TraceId = traceId?.Trim(),
            Timestamp = timestamp
        });
    }
}
