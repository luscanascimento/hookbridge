using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Simulator;
using HookBridge.Domain.Enums;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.Security;

public class AdversarialEndpointSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AdversarialEndpointSecurityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync(string slugPrefix)
    {
        var slug = $"{slugPrefix}-{Guid.NewGuid():N}"[..16];
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, $"Org {slugPrefix}", $"{slug}@adversary.test", "Password#2026"));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
    }

    private async Task<Guid> CreateAppAsync()
    {
        var res = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand(
            Name: "Default Test App",
            Description: "Security Test Application"
        ));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var app = await res.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);
        return app!.Id;
    }

    [Fact]
    public async Task CreateEndpoint_WithSsrfLoopbackTarget_ShouldReturnBadRequest()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("ssrfblock");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var appId = await CreateAppAsync();

        var command = new CreateEndpointCommand(
            ApplicationId: appId,
            TargetUrl: "http://127.0.0.1:5000/admin/secrets",
            Description: "Attempting SSRF against local service",
            RateLimitPerMinute: 60
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/endpoints", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Ssrf");
    }

    [Fact]
    public async Task CreateEndpoint_WithSsrfCloudMetadataTarget_ShouldReturnBadRequest()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("ssrfcloud");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var appId = await CreateAppAsync();

        var command = new CreateEndpointCommand(
            ApplicationId: appId,
            TargetUrl: "http://169.254.169.254/latest/meta-data/iam/security-credentials/",
            Description: "Attempting SSRF against cloud metadata",
            RateLimitPerMinute: 60
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/endpoints", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Ssrf");
    }

    [Fact]
    public async Task CrossTenantIdor_EndpointReadAndUpdate_ShouldReturnNotFoundForVictimResource()
    {
        // Arrange 1: Tenant A creates an endpoint
        var tokenA = await RegisterAndGetTokenAsync("tenant-a-ep");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var appAId = await CreateAppAsync();

        var createEpCmd = new CreateEndpointCommand(
            ApplicationId: appAId,
            TargetUrl: "https://api.github.com/webhooks/incoming",
            Description: "Private tenant A endpoint",
            RateLimitPerMinute: 100
        );
        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", createEpCmd);
        epRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var epA = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // Arrange 2: Tenant B tries to access Tenant A's endpoint
        var tokenB = await RegisterAndGetTokenAsync("tenant-b-ep");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        // Act 1: Tenant B tries to GET Tenant A's endpoint
        var getRes = await _client.GetAsync($"/api/v1/endpoints/{epA!.Id}");

        // Act 2: Tenant B tries to UPDATE Tenant A's endpoint
        var updateCmd = new UpdateEndpointCommand(
            TargetUrl: "https://evil.com/leak",
            Description: "Hijacked",
            RateLimitPerMinute: 10
        );
        var updateRes = await _client.PutAsJsonAsync($"/api/v1/endpoints/{epA.Id}", updateCmd);

        // Act 3: Tenant B tries to DELETE Tenant A's endpoint
        var deleteRes = await _client.DeleteAsync($"/api/v1/endpoints/{epA.Id}");

        // Assert: System must return 404 (or 403) and never mutate or leak Tenant A's data
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
        updateRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrossTenantIdor_SimulatorRuleReadAndDelete_ShouldReturnNotFoundForVictimRule()
    {
        // Arrange 1: Tenant A creates a simulator rule
        var tokenA = await RegisterAndGetTokenAsync("tenant-a-sim");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var createRuleCmd = new CreateSimulatorRuleCommand(
            Name: "Tenant A 500 Simulation",
            Slug: null,
            Description: "Tenant A rule",
            Strategy: SimulatorStrategy.FixedStatus,
            TargetStatusCode: 500,
            SuccessStatusCode: 200,
            FailureRatePercent: 0,
            FailureStepCount: 1,
            DelayMs: 0,
            ResponseHeadersJson: null,
            ResponseBody: null,
            ResponseContentType: "application/json"
        );
        var ruleRes = await _client.PostAsJsonAsync("/api/v1/simulator/rules", createRuleCmd);
        ruleRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var ruleA = await ruleRes.Content.ReadFromJsonAsync<SimulatorRuleResponse>(JsonOptions);

        // Arrange 2: Tenant B tries to access Tenant A's simulator rule
        var tokenB = await RegisterAndGetTokenAsync("tenant-b-sim");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        // Act 1: Tenant B tries to GET Tenant A's simulator rule
        var getRuleRes = await _client.GetAsync($"/api/v1/simulator/rules/{ruleA!.Id}");

        // Act 2: Tenant B tries to DELETE Tenant A's simulator rule
        var deleteRuleRes = await _client.DeleteAsync($"/api/v1/simulator/rules/{ruleA.Id}");

        // Assert: System must return 404
        getRuleRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
        deleteRuleRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
