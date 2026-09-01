using HookBridge.Application.Abstractions;

namespace HookBridge.Api.Middleware;

/// <summary>
/// Extracts tenant partition information from incoming HTTP headers or authenticated claims.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public const string TenantHeaderName = "X-Tenant-ID";

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // 1. Check for explicit Tenant Header (useful in machine-to-machine / API key context)
        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var tenantHeaderValues) &&
            !string.IsNullOrWhiteSpace(tenantHeaderValues.FirstOrDefault()))
        {
            var headerValue = tenantHeaderValues.First()!;
            if (Guid.TryParse(headerValue, out var parsedTenantGuid))
            {
                tenantContext.SetTenant(parsedTenantGuid, null);
            }
            else
            {
                tenantContext.SetTenant(Guid.Empty, headerValue.Trim());
            }
        }
        // 2. Check for authenticated JWT tenant claim
        else if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value
                ?? context.User.FindFirst("tid")?.Value;

            if (!string.IsNullOrWhiteSpace(tenantClaim) && Guid.TryParse(tenantClaim, out var parsedClaimGuid))
            {
                tenantContext.SetTenant(parsedClaimGuid, null);
            }
        }

        await _next(context);
    }
}
