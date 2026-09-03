using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Enums;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class ApiKeyEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiKeyEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ApiKey_Lifecycle_ShouldSucceed()
    {
        // Arrange: Register TenantAdmin
        var slug = $"key-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Key Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // 1. Create API Key
        var createCommand = new CreateApiKeyCommand("Production Pipeline Key", ApiKeyScope.EventsIngest | ApiKeyScope.DeliveriesRead, "live");
        var createRes = await _client.PostAsJsonAsync("/api/v1/api-keys", createCommand);

        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions);
        created.Should().NotBeNull();
        created!.Key.Should().StartWith("hb_live_");
        created.KeyPrefix.Should().Be(created.Key[..16]);

        // 2. List API Keys (should not leak full key)
        var listRes = await _client.GetAsync("/api/v1/api-keys");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var keys = await listRes.Content.ReadFromJsonAsync<IReadOnlyList<ApiKeyResponse>>(JsonOptions);
        keys.Should().NotBeNull();
        keys.Should().ContainSingle(k => k.Id == created.Id);
        keys![0].IsActive.Should().BeTrue();

        // 3. Revoke API Key
        var revokeRes = await _client.DeleteAsync($"/api/v1/api-keys/{created.Id}");
        revokeRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Verify Key is deactivated
        var listRes2 = await _client.GetAsync("/api/v1/api-keys");
        var keys2 = await listRes2.Content.ReadFromJsonAsync<IReadOnlyList<ApiKeyResponse>>(JsonOptions);
        keys2.Should().NotBeNull();
        keys2!.First(k => k.Id == created.Id).IsActive.Should().BeFalse();
        keys2!.First(k => k.Id == created.Id).RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ApiKey_Create_AsDeveloper_ShouldReturn403Forbidden()
    {
        // Arrange: 1. Register TenantAdmin
        var slug = $"dev-key-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Dev Key Org", $"{slug}@admin.test", "Password#2026"));
        var adminAuth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        // 2. Invite Developer
        var inviteReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/invite")
        {
            Content = JsonContent.Create(new InviteUserCommand($"dev@{slug}.test", UserRole.Developer, "Password#2026"))
        };
        inviteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        await _client.SendAsync(inviteReq);

        // 3. Login Developer
        var devLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand($"dev@{slug}.test", "Password#2026", slug));
        var devAuth = await devLogin.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        // 4. Developer attempts to create API Key (requires TenantAdmin)
        var devCreateReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-keys")
        {
            Content = JsonContent.Create(new CreateApiKeyCommand("Dev Key Attempt", ApiKeyScope.EventsIngest, "live"))
        };
        devCreateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", devAuth!.AccessToken);
        var devRes = await _client.SendAsync(devCreateReq);

        // Assert
        devRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
