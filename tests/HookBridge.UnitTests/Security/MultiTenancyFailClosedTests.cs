using FluentAssertions;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainApp = HookBridge.Domain.Entities.Application;

namespace HookBridge.UnitTests.Security;

public class MultiTenancyFailClosedTests
{
    [Fact]
    public async Task DbContext_WithoutTenantContext_MustBeStrictlyFailClosedAcrossAllEntities()
    {
        // Arrange
        var tenantContext = new TenantContext(); // HasTenant = false

        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var db = new HookBridgeDbContext(options, tenantContext);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var tenantA = Tenant.Create("tenant-alpha", "Tenant Alpha", now).Value;
        var tenantB = Tenant.Create("tenant-beta", "Tenant Beta", now).Value;

        var userA = User.Create(tenantA.Id, "admin@alpha.test", "hash1", UserRole.TenantAdmin, now).Value;
        var userB = User.Create(tenantB.Id, "admin@beta.test", "hash2", UserRole.TenantAdmin, now).Value;

        var appA = DomainApp.Create(tenantA.Id, "AlphaApp", "Alpha Description", now).Value;
        var epA = Endpoint.Create(tenantA.Id, appA.Id, "https://alpha.test/webhook", "Alpha EP", now, 100, 10).Value;
        var subA = Subscription.Create(tenantA.Id, epA.Id, "order.*", now).Value;
        var apiKeyA = ApiKey.Create(tenantA.Id, "Alpha Key", "hb_live_alph", "hash_alpha", ApiKeyScope.EventsIngest | ApiKeyScope.DeliveriesRead, now).Value;
        var secretA = WebhookSecret.Create(tenantA.Id, epA.Id, "whsec_alpha", "hash_sec_a", "enc_secret_a", 1, now).Value;
        var deliveryA = Delivery.Create(tenantA.Id, Guid.NewGuid(), epA.Id, subA.Id, "order.created", "corr_a", "00-trace-a", now).Value;

        db.Tenants.AddRange(tenantA, tenantB);
        db.Users.AddRange(userA, userB);
        db.Applications.Add(appA);
        db.Endpoints.Add(epA);
        db.Subscriptions.Add(subA);
        db.ApiKeys.Add(apiKeyA);
        db.WebhookSecrets.Add(secretA);
        db.Deliveries.Add(deliveryA);
        await db.SaveChangesAsync();

        // Act: Query all entities without setting tenant context (fail-closed check)
        var users = await db.Users.ToListAsync();
        var apps = await db.Applications.ToListAsync();
        var endpoints = await db.Endpoints.ToListAsync();
        var subscriptions = await db.Subscriptions.ToListAsync();
        var apiKeys = await db.ApiKeys.ToListAsync();
        var secrets = await db.WebhookSecrets.ToListAsync();
        var deliveries = await db.Deliveries.ToListAsync();

        // Assert: Zero records returned because tenant context is unauthenticated/unresolved
        users.Should().BeEmpty();
        apps.Should().BeEmpty();
        endpoints.Should().BeEmpty();
        subscriptions.Should().BeEmpty();
        apiKeys.Should().BeEmpty();
        secrets.Should().BeEmpty();
        deliveries.Should().BeEmpty();

        // Cross-tenant administrative queries explicitly using IgnoreQueryFilters must see all data
        var allUsers = await db.Users.IgnoreQueryFilters().ToListAsync();
        allUsers.Should().HaveCount(2);

        var allApps = await db.Applications.IgnoreQueryFilters().ToListAsync();
        allApps.Should().HaveCount(1);
    }

    [Fact]
    public async Task DbContext_WithTenantContext_MustOnlyReturnMatchingTenantEntities()
    {
        // Arrange
        var tenantContext = new TenantContext();

        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var db = new HookBridgeDbContext(options, tenantContext);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var tenantA = Tenant.Create("tenant-a-scoped", "Tenant A Scoped", now).Value;
        var tenantB = Tenant.Create("tenant-b-scoped", "Tenant B Scoped", now).Value;

        var appA = DomainApp.Create(tenantA.Id, "App A", null, now).Value;
        var appB = DomainApp.Create(tenantB.Id, "App B", null, now).Value;

        var epA = Endpoint.Create(tenantA.Id, appA.Id, "https://a.test/wh", "EP A", now, 100, 10).Value;
        var epB = Endpoint.Create(tenantB.Id, appB.Id, "https://b.test/wh", "EP B", now, 100, 10).Value;

        db.Tenants.AddRange(tenantA, tenantB);
        db.Applications.AddRange(appA, appB);
        db.Endpoints.AddRange(epA, epB);
        await db.SaveChangesAsync();

        // Act 1: Scope to Tenant A
        tenantContext.SetTenant(tenantA.Id, tenantA.Identifier);
        var appsA = await db.Applications.ToListAsync();
        var endpointsA = await db.Endpoints.ToListAsync();

        // Assert 1: Only Tenant A records returned
        appsA.Should().ContainSingle(a => a.Id == appA.Id);
        appsA.Should().NotContain(a => a.Id == appB.Id);
        endpointsA.Should().ContainSingle(e => e.Id == epA.Id);
        endpointsA.Should().NotContain(e => e.Id == epB.Id);

        // Act 2: Scope to Tenant B
        tenantContext.SetTenant(tenantB.Id, tenantB.Identifier);
        var appsB = await db.Applications.ToListAsync();
        var endpointsB = await db.Endpoints.ToListAsync();

        // Assert 2: Only Tenant B records returned
        appsB.Should().ContainSingle(a => a.Id == appB.Id);
        appsB.Should().NotContain(a => a.Id == appA.Id);
        endpointsB.Should().ContainSingle(e => e.Id == epB.Id);
        endpointsB.Should().NotContain(e => e.Id == epA.Id);
    }
}
