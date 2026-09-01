using HookBridge.Application.Abstractions;

namespace HookBridge.Infrastructure.MultiTenancy;

public sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;
    private string? _tenantIdentifier;

    public Guid? TenantId => _tenantId;
    public string? TenantIdentifier => _tenantIdentifier;
    public bool HasTenant => _tenantId.HasValue && _tenantId.Value != Guid.Empty;

    public void SetTenant(Guid tenantId, string? tenantIdentifier = null)
    {
        _tenantId = tenantId;
        _tenantIdentifier = tenantIdentifier;
    }
}
