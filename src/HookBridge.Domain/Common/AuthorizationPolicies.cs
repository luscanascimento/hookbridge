namespace HookBridge.Domain.Common;

public static class AuthorizationPolicies
{
    public const string RequireTenantAdmin = "RequireTenantAdmin";
    public const string RequireDeveloper = "RequireDeveloper";
    public const string RequireViewer = "RequireViewer";
    public const string RequireSystemOperator = "RequireSystemOperator";
}
