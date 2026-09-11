using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Enums;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.Security;

public class RbacAndTenantAuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RbacAndTenantAuthorizationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid TenantId, string Slug, Guid AdminId)> RegisterTenantAsync(string prefix)
    {
        var slug = $"{prefix}-{Guid.NewGuid():N}"[..16];
        var email = $"admin@{slug}.test";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, $"Tenant {prefix}", email, "Password#2026"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return (auth!.AccessToken, auth.User.TenantId, slug, auth.User.UserId);
    }

    private async Task<string> InviteAndLoginUserAsync(string adminToken, string email, UserRole role)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/invite")
        {
            Content = JsonContent.Create(new InviteUserCommand(email, role, "UserPass#2026"))
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var inviteRes = await _client.SendAsync(req);
        inviteRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(email, "UserPass#2026", null));
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
    }

    [Fact]
    public async Task UnauthenticatedRequest_WithTenantHeader_MustBeRejectedWithUnauthorized()
    {
        // Act: Attempt to access protected endpoint with forged X-Tenant-ID but no bearer token
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/apps");
        req.Headers.Add("X-Tenant-ID", Guid.NewGuid().ToString());

        var res = await _client.SendAsync(req);

        // Assert: Zero header trust ensures 401 Unauthorized
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ViewerRole_CannotPerformMutations_ReceivesForbidden()
    {
        // Arrange
        var (adminToken, _, slug, _) = await RegisterTenantAsync("rbac-vw");
        var viewerToken = await InviteAndLoginUserAsync(adminToken, $"viewer@{slug}.test", UserRole.Viewer);

        // Act 1: Viewer tries to create application -> 403 Forbidden
        var appReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/apps")
        {
            Content = JsonContent.Create(new CreateApplicationCommand("ViewerApp", null))
        };
        appReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);
        var appRes = await _client.SendAsync(appReq);
        appRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Act 2: Viewer tries to create API key -> 403 Forbidden
        var keyReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-keys")
        {
            Content = JsonContent.Create(new CreateApiKeyCommand("ViewerKey", ApiKeyScope.All, "live", null))
        };
        keyReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);
        var keyRes = await _client.SendAsync(keyReq);
        keyRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeveloperRole_CannotPerformTenantAdminActions_ReceivesForbidden()
    {
        // Arrange
        var (adminToken, _, slug, _) = await RegisterTenantAsync("rbac-dev");
        var devToken = await InviteAndLoginUserAsync(adminToken, $"dev@{slug}.test", UserRole.Developer);

        // Act 1: Developer CAN create application (Developer policy allows this)
        var appReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/apps")
        {
            Content = JsonContent.Create(new CreateApplicationCommand("DevApp", null))
        };
        appReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", devToken);
        var appRes = await _client.SendAsync(appReq);
        appRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act 2: Developer CANNOT invite new users (TenantAdmin policy required)
        var inviteReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/invite")
        {
            Content = JsonContent.Create(new InviteUserCommand($"another@{slug}.test", UserRole.Developer, "Pass#2026"))
        };
        inviteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", devToken);
        var inviteRes = await _client.SendAsync(inviteReq);
        inviteRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Act 3: Developer CANNOT list API keys (TenantAdmin policy required)
        var keyReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys");
        keyReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", devToken);
        var keyRes = await _client.SendAsync(keyReq);
        keyRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CrossTenant_IdorAttacks_MustBePreventedAndReturnNotFound()
    {
        // Arrange: Setup Tenant A with App and Endpoint
        var (tokenA, _, _, _) = await RegisterTenantAsync("idor-a");
        var (tokenB, _, _, _) = await RegisterTenantAsync("idor-b");

        // Tenant A creates an application
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/apps")
        {
            Content = JsonContent.Create(new CreateApplicationCommand("TargetAppA", null))
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var createRes = await _client.SendAsync(createReq);
        var appA = (await createRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions))!;

        // Act 1: Tenant B tries to read Tenant A's application by ID
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/apps/{appA.Id}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var getRes = await _client.SendAsync(getReq);

        // Assert 1: IDOR prevented, returned 404
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Act 2: Tenant B tries to delete Tenant A's application
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/apps/{appA.Id}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var delRes = await _client.SendAsync(delReq);

        // Assert 2: IDOR prevented, returned 404
        delRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
