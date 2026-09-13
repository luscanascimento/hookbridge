using System.Data.Common;
using HookBridge.Application.Abstractions;
using HookBridge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.IntegrationTests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private DbConnection? _sqliteConnection;

    /// <summary>
    /// Optional PostgreSQL connection string (e.g. from Testcontainers or external PostgreSQL instance).
    /// When empty or null, SQLite in-memory database is used for fast local execution without Docker dependency.
    /// </summary>
    public string? ConnectionString { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var testConfig = new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                ["ConnectionStrings:DefaultConnection"] = !string.IsNullOrWhiteSpace(ConnectionString)
                    ? ConnectionString
                    : "DataSource=:memory:",
                ["Jwt:Key"] = "Super_Secret_Test_Jwt_Key_Must_Be_At_Least_256_Bits_Long_2026!",
                ["Jwt:SecretKey"] = "Super_Secret_Test_Jwt_Key_Must_Be_At_Least_256_Bits_Long_2026!",
                ["Jwt:Issuer"] = "HookBridge.ControlPlane",
                ["Jwt:Audience"] = "HookBridge.DeveloperPortal",
                ["Jwt:AccessTokenExpirationMinutes"] = "15",
                ["Jwt:RefreshTokenExpirationDays"] = "7",
                ["WebhookEncryption:MasterKey"] = "7f8e9d0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6",
                ["EventFlow:BaseUrl"] = "http://localhost:5000",
                ["EventFlow:ApiKey"] = "test_eventflow_api_key_2026",
                ["EventFlow:TimeoutSeconds"] = "10",
                ["Ssrf:ResolveDns"] = "false",
                ["Cors:AllowedOrigins:0"] = "http://localhost:4200"
            };

            configBuilder.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove existing EF Core / Npgsql registrations
            var descriptorsToRemove = services
                .Where(d => d.ServiceType.FullName?.Contains("Npgsql", StringComparison.Ordinal) == true ||
                            d.ImplementationType?.FullName?.Contains("Npgsql", StringComparison.Ordinal) == true ||
                            d.ServiceType.FullName?.Contains("EntityFramework", StringComparison.Ordinal) == true ||
                            d.ImplementationType?.FullName?.Contains("EntityFramework", StringComparison.Ordinal) == true ||
                            d.ServiceType == typeof(HookBridgeDbContext) ||
                            d.ServiceType == typeof(DbContextOptions) ||
                            d.ServiceType == typeof(DbContextOptions<HookBridgeDbContext>))
                .ToList();

            foreach (var d in descriptorsToRemove)
            {
                services.Remove(d);
            }

            if (!string.IsNullOrWhiteSpace(ConnectionString))
            {
                services.AddDbContext<HookBridgeDbContext>(options =>
                {
                    options.UseNpgsql(ConnectionString);
                });
            }
            else
            {
                _sqliteConnection = new SqliteConnection("DataSource=:memory:");
                _sqliteConnection.Open();

                services.AddDbContext<HookBridgeDbContext>(options =>
                {
                    options.UseSqlite(_sqliteConnection);
                });
            }

            services.AddScoped<IHookBridgeDbContext>(sp => sp.GetRequiredService<HookBridgeDbContext>());

            // Replace IEventFlowClient with FakeEventFlowClient for testing
            var eventFlowDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventFlowClient));
            if (eventFlowDescriptor != null)
            {
                services.Remove(eventFlowDescriptor);
            }
            services.AddSingleton<FakeEventFlowClient>();
            services.AddSingleton<IEventFlowClient>(sp => sp.GetRequiredService<FakeEventFlowClient>());
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HookBridgeDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        try
        {
            if (_sqliteConnection != null)
            {
                await _sqliteConnection.DisposeAsync();
                _sqliteConnection = null;
            }
        }
        catch { }

        try
        {
            await base.DisposeAsync();
        }
        catch { }
    }
}
