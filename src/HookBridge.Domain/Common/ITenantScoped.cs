namespace HookBridge.Domain.Common;

/// <summary>
/// Marker interface for all entities isolated within a specific tenant boundary.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; }
}
