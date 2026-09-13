using System.Security.Claims;
using System.Text;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HookBridge.Infrastructure;

internal static class AuthenticationExtensions
{
    internal static IServiceCollection AddAuthenticationAndAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        // 4. JWT Authentication dynamically configured from IOptions<JwtOptions>
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOpts) =>
            {
                var jwt = jwtOpts.Value;
                var jwtKey = configuration["Jwt:Key"] 
                    ?? configuration["Jwt:SecretKey"]
                    ?? throw new InvalidOperationException(
                        "JWT signing key is not configured. Set 'Jwt:Key' in appsettings or environment variables.");
                var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

                options.RequireHttpsMetadata = false;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(Math.Min(jwt.ClockSkewSeconds, 60)),
                    RoleClaimType = ClaimTypes.Role,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
                };

                // Enable JWT authentication for browser cookies and WebSocket / SignalR connections via query parameter
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // 1. Cookie-based authentication (primary for browser clients)
                        if (context.Request.Cookies.TryGetValue("hb_access_token", out var cookieToken)
                            && !string.IsNullOrEmpty(cookieToken))
                        {
                            context.Token = cookieToken;
                            return Task.CompletedTask;
                        }

                        // 2. SignalR WebSocket authentication via query parameter
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        // 5. Role-Based Authorization Policies
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.RequireTenantAdmin, policy =>
                policy.RequireRole(UserRole.TenantAdmin.ToString(), UserRole.SystemOperator.ToString()))
            .AddPolicy(AuthorizationPolicies.RequireDeveloper, policy =>
                policy.RequireRole(UserRole.Developer.ToString(), UserRole.TenantAdmin.ToString(), UserRole.SystemOperator.ToString()))
            .AddPolicy(AuthorizationPolicies.RequireViewer, policy =>
                policy.RequireRole(UserRole.Viewer.ToString(), UserRole.Developer.ToString(), UserRole.TenantAdmin.ToString(), UserRole.SystemOperator.ToString()))
            .AddPolicy(AuthorizationPolicies.RequireSystemOperator, policy =>
                policy.RequireRole(UserRole.SystemOperator.ToString()));

        return services;
    }
}
