using FluentAssertions;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.UnitTests.Infrastructure;

public class GlobalQueryFilterTests
{
    [Fact]
    public async Task QueryFilter_ShouldFilterUsersByCurrentTenant()
    {
        var tenantContext = new TenantContext();

        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var db = new HookBridgeDbContext(options, tenantContext);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var tenantA = Tenant.Create("tenant-a", "Tenant A", now).Value;
        var tenantB = Tenant.Create("tenant-b", "Tenant B", now).Value;

        var userA = User.Create(tenantA.Id, "usera@test.com", "hash", UserRole.Developer, now).Value;
        var userB = User.Create(tenantB.Id, "userb@test.com", "hash", UserRole.Developer, now).Value;

        db.Tenants.AddRange(tenantA, tenantB);
        db.Users.AddRange(userA, userB);
        await db.SaveChangesAsync();

        // 1. Without tenant set -> sees all
        var allUsers = await db.Users.ToListAsync();
        allUsers.Should().HaveCount(2);

        // 2. Set Tenant A
        tenantContext.SetTenant(tenantA.Id, tenantA.Identifier);
        var usersA = await db.Users.ToListAsync();
        usersA.Should().ContainSingle(u => u.Email == "usera@test.com");
        usersA.Should().NotContain(u => u.Email == "userb@test.com");

        // 3. Set Tenant B
        tenantContext.SetTenant(tenantB.Id, tenantB.Identifier);
        var usersB = await db.Users.ToListAsync();
        usersB.Should().ContainSingle(u => u.Email == "userb@test.com");
        usersB.Should().NotContain(u => u.Email == "usera@test.com");
    }
}
