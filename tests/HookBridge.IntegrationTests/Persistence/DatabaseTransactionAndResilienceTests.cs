using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Common;
using HookBridge.Domain.Entities;
using HookBridge.Infrastructure.Persistence;
using HookBridge.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.IntegrationTests.Persistence;

public sealed class DatabaseTransactionAndResilienceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DatabaseTransactionAndResilienceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DatabaseTransaction_WhenRolledBack_EnsuresZeroPartialStatePersisted()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HookBridgeDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        
        var tenant = Tenant.Create($"tx-{Guid.NewGuid():N}", "Tx Tenant", DateTimeOffset.UtcNow).Value;
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        tenantContext.SetTenant(tenant.Id, tenant.Identifier);

        var appName = $"Tx-App-{Guid.NewGuid():N}";
        var app = HookBridge.Domain.Entities.Application.Create(tenant.Id, appName, "Transaction test", DateTimeOffset.UtcNow).Value;

        // Act: Insert inside transaction and explicitly rollback
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            db.Applications.Add(app);
            await db.SaveChangesAsync();

            await tx.RollbackAsync();
        }

        // Assert: Application was NOT persisted
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<HookBridgeDbContext>();
        var foundApp = await verifyDb.Applications.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Name == appName);
        foundApp.Should().BeNull("Rolled back transaction must leave no trace in database.");
    }

    [Fact]
    public async Task DatabaseTransaction_WhenCommitted_PersistsAllChangesAtomically()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HookBridgeDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        var tenant = Tenant.Create($"tx-commit-{Guid.NewGuid():N}", "Tx Commit Tenant", DateTimeOffset.UtcNow).Value;
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        tenantContext.SetTenant(tenant.Id, tenant.Identifier);

        var appName = $"Tx-Commit-{Guid.NewGuid():N}";
        var app = HookBridge.Domain.Entities.Application.Create(tenant.Id, appName, "Commit test", DateTimeOffset.UtcNow).Value;

        // Act: Insert inside transaction and commit
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            db.Applications.Add(app);
            await db.SaveChangesAsync();

            await tx.CommitAsync();
        }

        // Assert: Application is successfully persisted
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<HookBridgeDbContext>();
        var foundApp = await verifyDb.Applications.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Name == appName);
        foundApp.Should().NotBeNull();
        foundApp!.Name.Should().Be(appName);
        foundApp.TenantId.Should().Be(tenant.Id);
    }

    [Fact]
    public async Task Database_MultiTenantQueryFilter_StrictlyIsolatesCrossTenantQueries()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var tenantA = Tenant.Create($"tenant-a-{Guid.NewGuid():N}", "Tenant A", now).Value;
        var tenantB = Tenant.Create($"tenant-b-{Guid.NewGuid():N}", "Tenant B", now).Value;

        using (var setupScope = _factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<HookBridgeDbContext>();
            db.Tenants.AddRange(tenantA, tenantB);
            await db.SaveChangesAsync();

            var appA = HookBridge.Domain.Entities.Application.Create(tenantA.Id, $"AppA-{Guid.NewGuid():N}", "Tenant A app", now).Value;
            var appB = HookBridge.Domain.Entities.Application.Create(tenantB.Id, $"AppB-{Guid.NewGuid():N}", "Tenant B app", now).Value;

            db.Applications.AddRange(appA, appB);
            await db.SaveChangesAsync();
        }

        // Act: Query with Tenant A context
        using var queryScopeA = _factory.Services.CreateScope();
        var tenantContextA = queryScopeA.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContextA.SetTenant(tenantA.Id, tenantA.Identifier);

        var dbA = queryScopeA.ServiceProvider.GetRequiredService<HookBridgeDbContext>();
        var appsForTenantA = await dbA.Applications.ToListAsync();

        // Assert: Only Tenant A's applications are returned
        appsForTenantA.Should().NotBeEmpty();
        appsForTenantA.Should().OnlyContain(a => a.TenantId == tenantA.Id);
        appsForTenantA.Any(a => a.TenantId == tenantB.Id).Should().BeFalse("Tenant A must never see Tenant B data.");
    }
}
