using System.Diagnostics;
using System.Security.Claims;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Diagnostics;
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
        var clientIp = context.Connection.RemoteIpAddress?.ToString();
        var traceId = Activity.Current?.TraceId.ToString() ?? Activity.Current?.Id ?? context.TraceIdentifier;

        if (currentUser is CurrentUser mutableUser)
        {
            mutableUser.IpAddress = clientIp;
            mutableUser.TraceId = traceId;
        }

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

            if (currentUser is CurrentUser authUser)
            {
                if (Guid.TryParse(subClaim, out var parsedUserId))
                {
                    authUser.UserId = parsedUserId;
                }

                authUser.Email = emailClaim;

                if (Enum.TryParse<UserRole>(roleClaim, true, out var parsedRole))
                {
                    authUser.Role = parsedRole;
                }
            }

            if (!string.IsNullOrWhiteSpace(tenantClaim) && Guid.TryParse(tenantClaim, out var parsedTenantId))
            {
                tenantContext.SetTenant(parsedTenantId, tenantSlugClaim);
                Activity.Current?.SetTag(HookBridgeDiagnostics.TagTenantId, parsedTenantId.ToString());
                Activity.Current?.SetBaggage(HookBridgeDiagnostics.TagTenantId, parsedTenantId.ToString());
            }
        }
        // 2. Zero Header Trust: Unauthenticated requests cannot set or spoof tenant context via client-sent headers.
        // Tenant context is strictly bound to cryptographically verified claims from authenticated identities.

        await _next(context);
    }
}
