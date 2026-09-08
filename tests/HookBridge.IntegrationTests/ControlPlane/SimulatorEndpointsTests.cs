using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Simulator;
using HookBridge.Domain.Enums;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class SimulatorEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SimulatorEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync(string? slugPrefix = null)
    {
        var slug = $"{slugPrefix ?? "sim"}-{Guid.NewGuid():N}"[..16];
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Simulator Org", $"{slug}@test.com", "Password#2026"));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
    }

    [Fact]
    public async Task CreateSimulatorRule_ShouldReturnCreatedRule()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("simcreate");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var command = new CreateSimulatorRuleCommand(
            Name: "Test 429 Throttle Rule",
            Slug: null,
            Description: "Simulates rate limits",
            Strategy: SimulatorStrategy.FixedStatus,
            TargetStatusCode: 429,
            SuccessStatusCode: 200,
            FailureRatePercent: 0,
            FailureStepCount: 1,
            DelayMs: 0,
            ResponseHeadersJson: "{\"Retry-After\": \"30\"}",
            ResponseBody: "{\"error\": \"Rate limit exceeded\"}",
            ResponseContentType: "application/json"
        );

        // Act
        var res = await _client.PostAsJsonAsync("/api/v1/simulator/rules", command);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await res.Content.ReadFromJsonAsync<SimulatorRuleResponse>(JsonOptions);
        created.Should().NotBeNull();
        created!.Name.Should().Be("Test 429 Throttle Rule");
        created.Slug.Should().NotBeNullOrWhiteSpace();
        created.ReceiverUrl.Should().Contain($"/api/v1/simulator/receive/{created.Slug}");
        created.TargetStatusCode.Should().Be(429);
    }

    [Fact]
    public async Task ReceiveSimulatorWebhook_PublicEndpoint_ShouldExecuteRuleAndSimulateResponse()
    {
        // Arrange - Create Rule
        var token = await RegisterAndGetTokenAsync("simrecv");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var slug = $"chaos-rule-{Guid.NewGuid():N}"[..18];
        var createRes = await _client.PostAsJsonAsync("/api/v1/simulator/rules", new CreateSimulatorRuleCommand(
            Name: "500 Server Error Sim",
            Slug: slug,
            Strategy: SimulatorStrategy.FixedStatus,
            TargetStatusCode: 500,
            ResponseBody: "{\"error\":\"custom 500 error\"}"
        ));
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var rule = await createRes.Content.ReadFromJsonAsync<SimulatorRuleResponse>(JsonOptions);

        // Act - Invoke Public Receiver without Auth
        var receiverClient = _factory.CreateClient();
        var webhookContent = new StringContent("{\"event\":\"payment.failed\"}", System.Text.Encoding.UTF8, "application/json");

        var receiverRes = await receiverClient.PostAsync($"/api/v1/simulator/receive/{rule!.Slug}", webhookContent);

        // Assert Receiver Response
        receiverRes.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var simBody = await receiverRes.Content.ReadAsStringAsync();
        simBody.Should().Contain("custom 500 error");

        // Act - Fetch Executions as Tenant
        var executionsRes = await _client.GetAsync($"/api/v1/simulator/executions?ruleId={rule.Id}");
        executionsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await executionsRes.Content.ReadFromJsonAsync<PagedSimulatorExecutionsResponse>(JsonOptions);

        paged.Should().NotBeNull();
        paged!.Items.Should().HaveCount(1);
        paged.Items[0].SimulatedStatusCode.Should().Be(500);
    }

    [Fact]
    public async Task ReceiveAdHocStatusWebhook_ShouldReturn429WithRetryAfter()
    {
        // Arrange
        var receiverClient = _factory.CreateClient();

        // Act
        var res = await receiverClient.PostAsync("/api/v1/simulator/http/429?retryAfter=45", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        res.Headers.Should().Contain(h => h.Key == "Retry-After");
    }

    [Fact]
    public async Task ReceiveAdHocTimeoutWebhook_ShouldReturn504()
    {
        // Arrange
        var receiverClient = _factory.CreateClient();

        // Act
        var res = await receiverClient.PostAsync("/api/v1/simulator/timeout?delay=10", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
    }

    [Fact]
    public async Task TestDispatchEndpoint_ShouldTriggerDirectSimulation()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("simdisp");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var command = new TestDispatchCommand(
            AdHocStatusCode: 400,
            AdHocDelayMs: 0,
            PayloadBody: "{\"test\": true}"
        );

        // Act
        var res = await _client.PostAsJsonAsync("/api/v1/simulator/test-dispatch", command);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var sim = await res.Content.ReadFromJsonAsync<SimulatedExecutionResult>(JsonOptions);
        sim.Should().NotBeNull();
        sim!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetSimulatorStatsEndpoint_ShouldReturnStats()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("simstats");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var res = await _client.GetAsync("/api/v1/simulator/stats");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await res.Content.ReadFromJsonAsync<SimulatorStatsResponse>(JsonOptions);
        stats.Should().NotBeNull();
    }
}
