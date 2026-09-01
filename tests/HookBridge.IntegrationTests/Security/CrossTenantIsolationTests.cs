using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Persistence;
using HookBridge.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.IntegrationTests.Security;

public class CrossTenantIsolationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CrossTenantIsolationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AuthenticatedUser_CannotSpoofTenantIdViaHeader()
    {
        // Arrange 1: Register Tenant A
        var slugA = $"spoof-a-{Guid.NewGuid():N}"[..16];
        var regResponseA = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slugA, "Tenant A", $"admin@{slugA}.test", "Password#2026"));
        var authA = await regResponseA.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        // Arrange 2: Register Tenant B
        var slugB = $"spoof-b-{Guid.NewGuid():N}"[..16];
        var regResponseB = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slugB, "Tenant B", $"admin@{slugB}.test", "Password#2026"));
        var authB = await regResponseB.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        // Act: User from Tenant B sends request with Tenant A's ID in X-Tenant-ID header
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authB!.AccessToken);
        request.Headers.Add("X-Tenant-ID", authA!.User.TenantId.ToString());

        var response = await _client.SendAsync(request);

        // Assert: System must respect JWT claims, rejecting the forged header override
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);
        profile.Should().NotBeNull();
        profile!.TenantId.Should().Be(authB.User.TenantId);
        profile.TenantIdentifier.Should().Be(slugB);
        profile.TenantId.Should().NotBe(authA.User.TenantId);
    }

    [Fact]
    public async Task EFCore_GlobalQueryFilter_PreventsCrossTenantDataRead()
    {
        // Arrange: Direct DB setup with two tenants
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HookBridgeDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        var now = DateTimeOffset.UtcNow;

        var tenantA = Tenant.Create("ef-tenant-a", "EF Tenant A", now).Value;
        var tenantB = Tenant.Create("ef-tenant-b", "EF Tenant B", now).Value;

        var userA = User.Create(tenantA.Id, "usera@ef.test", "hash", UserRole.Developer, now).Value;
        var userB = User.Create(tenantB.Id, "userb@ef.test", "hash", UserRole.Developer, now).Value;

        db.Tenants.AddRange(tenantA, tenantB);
        db.Users.AddRange(userA, userB);
        await db.SaveChangesAsync();

        // Act: Scope to Tenant A
        tenantContext.SetTenant(tenantA.Id, tenantA.Identifier);

        // Query users through EF Core with global query filter active
        var visibleUsersForTenantA = await db.Users.ToListAsync();

        // Assert: Tenant A can only see user A, never user B
        visibleUsersForTenantA.Should().ContainSingle(u => u.Email == "usera@ef.test");
        visibleUsersForTenantA.Should().NotContain(u => u.Email == "userb@ef.test");
    }
}
