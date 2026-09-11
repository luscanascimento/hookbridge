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
        // 2. Zero Header Trust: Unauthenticated requests cannot set or spoof tenant context via client-sent headers.
        // Tenant context is strictly bound to cryptographically verified claims from authenticated identities.

        await _next(context);
    }
}
