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

        // 2. Cryptographic & Auth Services with Startup Validation
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<WebhookEncryptionOptions>()
            .BindConfiguration(WebhookEncryptionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SsrfOptions>()
            .BindConfiguration(SsrfOptions.SectionName);

        services.AddOptions<HookBridge.Infrastructure.Integration.EventFlowOptions>()
            .BindConfiguration(HookBridge.Infrastructure.Integration.EventFlowOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<HookBridge.Infrastructure.Resilience.ResilienceOptions>()
            .BindConfiguration(HookBridge.Infrastructure.Resilience.ResilienceOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<ISecretEncryptor, AesSecretEncryptor>();
        services.AddSingleton<IApiKeyGenerator, KeyGenerator>();
        services.AddSingleton<ISsrfGuard, SsrfGuard>();
        services.AddSingleton<IWebhookSigner, WebhookSigner>();
        services.AddSingleton<HookBridge.Infrastructure.Resilience.IHttpResiliencePipelineProvider, HookBridge.Infrastructure.Resilience.HttpResiliencePipelineProvider>();

        // 3. EventFlow Integration HTTP Client
        services.AddHttpClient<IEventFlowClient, HookBridge.Infrastructure.Integration.EventFlowClient>();

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
                var secretKey = !string.IsNullOrWhiteSpace(jwt.SecretKey) ? jwt.SecretKey : "Fallback_Temp_Key_For_Options_Configuration_32_Bytes!";
                var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

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
                    ClockSkew = TimeSpan.FromSeconds(Math.Min(jwt.ClockSkewSeconds, 60)),
                    RoleClaimType = ClaimTypes.Role,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
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

        // 6. PostgreSQL Persistence with Strict Production Validation
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var envName = configuration["ASPNETCORE_ENVIRONMENT"] 
            ?? configuration["DOTNET_ENVIRONMENT"] 
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (string.Equals(envName, "Production", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Critical configuration missing: 'ConnectionStrings:DefaultConnection' must be set in Production.");
            }

            connectionString = "Host=localhost;Port=5432;Database=hookbridge_db;Username=postgres;Password=postgres";
        }

        services.AddDbContext<HookBridgeDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(HookBridgeDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
                npgsqlOptions.CommandTimeout(30);
            });
        });

        services.AddScoped<IHookBridgeDbContext>(sp => sp.GetRequiredService<HookBridgeDbContext>());

        // 7. Observability & Telemetry
        services.AddHookBridgeTelemetry(configuration);

        return services;
    }
}
