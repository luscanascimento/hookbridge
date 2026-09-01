namespace HookBridge.Application.Abstractions;

/// <summary>
/// Provides access to the current authenticated tenant context for request-scoped operations.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    string? TenantIdentifier { get; }
    bool HasTenant => TenantId.HasValue && TenantId.Value != Guid.Empty;
    void SetTenant(Guid tenantId, string? tenantIdentifier = null);
}
