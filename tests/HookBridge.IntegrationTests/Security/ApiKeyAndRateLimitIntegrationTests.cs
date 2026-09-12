using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.Integration.DTOs;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.Security;

public sealed class ApiKeyAndRateLimitIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiKeyAndRateLimitIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IngestionApi_WhenUsingValidApiKey_AuthenticatesAndAcceptsEvent()
    {
        // 1. Register a tenant and obtain JWT
        var slug = $"apikey-test-{Guid.NewGuid():N}"[..18];
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "API Key Corp", $"{slug}@example.test", "Password#2026"));
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        // 2. Create an API Key as Developer/Admin
        using var authClient = _factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var keyRes = await authClient.PostAsJsonAsync("/api/v1/api-keys", new CreateApiKeyCommand(
            "Service Ingestion Key", HookBridge.Domain.Enums.ApiKeyScope.EventsIngest, "live", null));
        keyRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdKey = await keyRes.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions);
        createdKey.Should().NotBeNull();
        createdKey!.Key.Should().StartWith("hb_live_");

        // 3. Make an unauthenticated request to /api/v1/events using ONLY X-Api-Key
        using var machineClient = _factory.CreateClient();
        machineClient.DefaultRequestHeaders.Add("X-Api-Key", createdKey.Key);

        var payload = JsonDocument.Parse("{\"sensor\":\"temp\",\"reading\":23.5}").RootElement;
        var publishCommand = new PublishEventCommand("sensor.reading", payload);

        var publishRes = await machineClient.PostAsJsonAsync("/api/v1/events", publishCommand);

        // Assert: Request accepted via API Key!
        publishRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var publishBody = await publishRes.Content.ReadFromJsonAsync<PublishEventResponse>(JsonOptions);
        publishBody.Should().NotBeNull();
        publishBody!.EventType.Should().Be("sensor.reading");
    }

    [Fact]
    public async Task AuthLogout_RevokesRefreshToken_PreventingFurtherRefresh()
    {
        // 1. Register and login
        var slug = $"logout-test-{Guid.NewGuid():N}"[..18];
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Logout Corp", $"{slug}@example.test", "Password#2026"));
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        // 2. Call /api/v1/auth/logout with the refresh token
        using var logoutClient = _factory.CreateClient();
        logoutClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var logoutRes = await logoutClient.PostAsJsonAsync("/api/v1/auth/logout", new LogoutCommand(auth.RefreshToken));
        logoutRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Attempting to use the revoked refresh token must be rejected
        var refreshRes = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenCommand(auth.RefreshToken));
        refreshRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
