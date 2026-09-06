using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class EndpointHealthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EndpointHealthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task EndpointHealthEndpoints_RequireAuthorization_ShouldReturn401WhenUnauthenticated()
    {
        // Act
        var res1 = await _client.GetAsync("/api/v1/endpoints/health");
        var res2 = await _client.GetAsync($"/api/v1/endpoints/{Guid.NewGuid()}/health");

        // Assert
        res1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        res2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EndpointHealthEndpoints_CompleteLifecycle_SummaryAndDeepMetrics()
    {
        // Arrange - Register & authenticate
        var slug = $"hlth-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Health Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Create App & Endpoint
        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Health App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.github.com/webhook", "Health Monitored Endpoint", 600, 15, new List<string> { "order.*" }));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // 1. Query Tenant Endpoints Health Summary
        var summaryRes = await _client.GetAsync("/api/v1/endpoints/health");
        summaryRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await summaryRes.Content.ReadFromJsonAsync<TenantEndpointsHealthSummaryResponse>(JsonOptions);
        summary.Should().NotBeNull();
        summary!.TotalEndpoints.Should().Be(1);
        summary.HealthyCount.Should().Be(1);
        summary.OverallHealthScorePercent.Should().BeGreaterThanOrEqualTo(90);
        summary.Endpoints.Should().ContainSingle();

        // 2. Query Deep Endpoint Health Details
        var detailRes = await _client.GetAsync($"/api/v1/endpoints/{ep!.Id}/health");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailRes.Content.ReadFromJsonAsync<EndpointHealthResponse>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.EndpointId.Should().Be(ep.Id);
        detail.Status.Should().Be("Active");
        detail.CircuitState.Should().Be("Closed");
        detail.HourlyBuckets.Should().NotBeEmpty();
        detail.HourlyBuckets.Count.Should().BeGreaterThanOrEqualTo(24);
    }
}
