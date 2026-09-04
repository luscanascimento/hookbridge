using System.Security.Claims;
using System.Text;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using HookBridge.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HookBridge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Multi-Tenancy & Actor Identity
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // 2. Cryptographic & Auth Services
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<WebhookEncryptionOptions>(configuration.GetSection(WebhookEncryptionOptions.SectionName));
        services.Configure<SsrfOptions>(configuration.GetSection(SsrfOptions.SectionName));
        services.Configure<HookBridge.Infrastructure.Integration.EventFlowOptions>(configuration.GetSection(HookBridge.Infrastructure.Integration.EventFlowOptions.SectionName));

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<ISecretEncryptor, AesSecretEncryptor>();
        services.AddSingleton<IApiKeyGenerator, KeyGenerator>();
        services.AddSingleton<ISsrfGuard, SsrfGuard>();
        services.AddSingleton<IWebhookSigner, WebhookSigner>();

        // 3. EventFlow Integration HTTP Client
        services.AddHttpClient<IEventFlowClient, HookBridge.Infrastructure.Integration.EventFlowClient>();

        // 3. JWT Authentication dynamically configured from IOptions<JwtOptions>
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
                var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey));

                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = ClaimTypes.Role
                };

                // Enable JWT authentication for WebSocket / SignalR connections via query parameter (?access_token=...)
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
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

        // 4. Role-Based Authorization Policies
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.RequireTenantAdmin, policy =>
                policy.RequireRole(UserRole.TenantAdmin.ToString(), UserRole.SystemOperator.ToString()))
            .AddPolicy(AuthorizationPolicies.RequireDeveloper, policy =>
                policy.RequireRole(UserRole.Developer.ToString(), UserRole.TenantAdmin.ToString(), UserRole.SystemOperator.ToString()))
            .AddPolicy(AuthorizationPolicies.RequireViewer, policy =>
                policy.RequireRole(UserRole.Viewer.ToString(), UserRole.Developer.ToString(), UserRole.TenantAdmin.ToString(), UserRole.SystemOperator.ToString()))
            .AddPolicy(AuthorizationPolicies.RequireSystemOperator, policy =>
                policy.RequireRole(UserRole.SystemOperator.ToString()));

        // 5. PostgreSQL Persistence
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=hookbridge_db;Username=postgres;Password=postgres";

        services.AddDbContext<HookBridgeDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(HookBridgeDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            });
        });

        services.AddScoped<IHookBridgeDbContext>(sp => sp.GetRequiredService<HookBridgeDbContext>());

        // 6. Observability & Telemetry
        services.AddHookBridgeTelemetry(configuration);

        return services;
    }
}
