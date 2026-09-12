using System.Security.Claims;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Api.Middleware;

/// <summary>
/// Authenticates requests bearing API Keys via 'X-Api-Key' or 'Authorization: ApiKey ...' / 'Authorization: Bearer hb_...'.
/// Populates ClaimsPrincipal with Developer role and TenantId for seamless authorization and tenant isolation.
/// </summary>
public sealed class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public const string ApiKeyHeaderName = "X-Api-Key";

    public ApiKeyAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IHookBridgeDbContext dbContext,
        IApiKeyGenerator keyGenerator,
        IDateTimeProvider dateTimeProvider)
    {
        var rawApiKey = ExtractApiKey(context);

        if (!string.IsNullOrWhiteSpace(rawApiKey))
        {
            var keyHash = keyGenerator.ComputeHash(rawApiKey);
            var now = dateTimeProvider.UtcNow;

            var apiKey = await dbContext.ApiKeys
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(k => k.KeyHash == keyHash, context.RequestAborted);

            if (apiKey == null || !apiKey.IsActive)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "The provided API Key is invalid, expired, or has been revoked.",
                    Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                    Extensions = { ["errorCode"] = "Auth.InvalidApiKey" }
                };
                await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
                return;
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, apiKey.Id.ToString()),
                new(ClaimTypes.Role, UserRole.Developer.ToString()),
                new("role", UserRole.Developer.ToString()),
                new("tenant_id", apiKey.TenantId.ToString()),
                new("scope", apiKey.Scopes.ToString()),
                new("auth_type", "api_key")
            };

            var identity = new ClaimsIdentity(claims, "ApiKey");
            context.User = new ClaimsPrincipal(identity);
        }

        await _next(context);
    }

    private static string? ExtractApiKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeader) &&
            !string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return apiKeyHeader.ToString().Trim();
        }

        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authVal = authHeader.ToString().Trim();
            if (authVal.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
            {
                return authVal["ApiKey ".Length..].Trim();
            }

            if (authVal.StartsWith("Bearer hb_", StringComparison.OrdinalIgnoreCase))
            {
                return authVal["Bearer ".Length..].Trim();
            }
        }

        return null;
    }
}
