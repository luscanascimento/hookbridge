using HookBridge.Application.Abstractions;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Security;
using HookBridge.Infrastructure.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Multi-Tenancy & Actor Identity
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // 2. Security, Cryptography & Options
        services.AddSecurityServices(configuration);

        // 3. EventFlow Integration HTTP Client
        services.AddHttpClient<IEventFlowClient, HookBridge.Infrastructure.Integration.EventFlowClient>();

        // 4. Authentication & Authorization
        services.AddAuthenticationAndAuthorization(configuration);

        // 5. PostgreSQL Persistence
        services.AddPersistence(configuration);

        // 6. Observability & Telemetry
        services.AddHookBridgeTelemetry(configuration);

        return services;
    }
}
