using System.Diagnostics;
using HookBridge.Domain.Common;
using HookBridge.Domain.Security;

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

        var resolvedTraceId = !string.IsNullOrWhiteSpace(traceId)
            ? traceId.Trim()
            : (Activity.Current?.TraceId.ToString() ?? Activity.Current?.Id);

        var sanitizedDetails = SensitiveDataSanitizer.SanitizeJson(detailsJson);

        return Result.Success(new AuditEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Action = action.Trim(),
            ResourceType = resourceType.Trim(),
            ResourceId = resourceId.Trim(),
            DetailsJson = sanitizedDetails,
            IpAddress = ipAddress?.Trim(),
            TraceId = resolvedTraceId,
            Timestamp = timestamp
        });
    }
}
