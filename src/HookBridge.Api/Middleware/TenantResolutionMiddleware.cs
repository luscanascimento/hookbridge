using System.Security.Claims;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Security;

namespace HookBridge.Api.Middleware;

/// <summary>
/// Extracts tenant partition information and authenticated identity into scoped context services.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public const string TenantHeaderName = "X-Tenant-ID";

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ICurrentUser currentUser)
    {
        // 1. Check for authenticated JWT user identity and claims
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;

            var emailClaim = context.User.FindFirst(ClaimTypes.Email)?.Value
                ?? context.User.FindFirst("email")?.Value;

            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value
                ?? context.User.FindFirst("role")?.Value;

            var tenantClaim = context.User.FindFirst("tenant_id")?.Value
                ?? context.User.FindFirst("tid")?.Value;

            var tenantSlugClaim = context.User.FindFirst("tenant_slug")?.Value;

            if (currentUser is CurrentUser mutableUser)
            {
                if (Guid.TryParse(subClaim, out var parsedUserId))
                {
                    mutableUser.UserId = parsedUserId;
                }

                mutableUser.Email = emailClaim;

                if (Enum.TryParse<UserRole>(roleClaim, true, out var parsedRole))
                {
                    mutableUser.Role = parsedRole;
                }
            }

            if (!string.IsNullOrWhiteSpace(tenantClaim) && Guid.TryParse(tenantClaim, out var parsedTenantId))
            {
                tenantContext.SetTenant(parsedTenantId, tenantSlugClaim);
            }
        }
        // 2. Check for explicit Tenant Header (e.g., in machine-to-machine / API key context)
        else if (context.Request.Headers.TryGetValue(TenantHeaderName, out var tenantHeaderValues) &&
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

        await _next(context);
    }
}
