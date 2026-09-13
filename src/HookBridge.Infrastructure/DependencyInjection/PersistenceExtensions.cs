using HookBridge.Application.Abstractions;
using HookBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.Infrastructure;

internal static class PersistenceExtensions
{
    internal static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
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

        return services;
    }
}
