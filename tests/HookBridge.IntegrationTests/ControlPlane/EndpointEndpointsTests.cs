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

public class EndpointEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EndpointEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync(string? slugPrefix = null)
    {
        var slug = $"{slugPrefix ?? "ep"}-{Guid.NewGuid():N}"[..16];
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Endpoint Org", $"{slug}@test.com", "Password#2026"));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
    }

    [Fact]
    public async Task CreateEndpoint_ShouldReturnInitialSecret_AndProvisionActiveStatus()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("endpoint");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Create Application
        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Store App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        // 2. Create Endpoint
        var createCommand = new CreateEndpointCommand(
            app!.Id,
            "https://api.github.com/webhooks/incoming",
            "GitHub Incoming Hook",
            600,
            10,
            new List<string> { "push", "pull_request" });

        var res = await _client.PostAsJsonAsync("/api/v1/endpoints", createCommand);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await res.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);
        created.Should().NotBeNull();
        created!.InitialSecret.Should().StartWith("whsec_");
        created.SecretPrefix.Should().StartWith("whsec_");
        created.SecretVersion.Should().Be(1);
        created.SubscribedEvents.Should().Contain("push");
        created.SubscribedEvents.Should().Contain("pull_request");

        // 3. Get Endpoint details
        var getRes = await _client.GetAsync($"/api/v1/endpoints/{created.Id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var endpointDetails = await getRes.Content.ReadFromJsonAsync<EndpointResponse>(JsonOptions);
        endpointDetails!.ActiveSecretPrefix.Should().Be(created.SecretPrefix);
        endpointDetails.Status.Should().Be(EndpointStatus.Active);
    }

    [Fact]
    public async Task CreateEndpoint_WithSsrfTarget_ShouldReturn400BadRequest()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("ssrf");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("SSRF App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var createCommand = new CreateEndpointCommand(
            app!.Id,
            "http://127.0.0.1:8080/internal/admin",
            "SSRF attempt");

        // Act
        var res = await _client.PostAsJsonAsync("/api/v1/endpoints", createCommand);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateEndpointStatus_ShouldChangeStatusSuccessfully()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("status");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Status App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.github.com/webhook", "Test"));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // Act - Pause Endpoint
        var patchRes = await _client.PatchAsJsonAsync($"/api/v1/endpoints/{ep!.Id}/status", new UpdateEndpointStatusCommand(
            EndpointStatus.Paused, "Upgrading server"));

        // Assert
        patchRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await patchRes.Content.ReadFromJsonAsync<EndpointResponse>(JsonOptions);
        updated!.Status.Should().Be(EndpointStatus.Paused);
        updated.DisabledReason.Should().Be("Upgrading server");
    }
}
