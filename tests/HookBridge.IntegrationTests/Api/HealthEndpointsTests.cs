using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.Api;

public class HealthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LivenessCheck_ShouldReturnOk_WithHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("status").GetString().Should().Be("Healthy");
        json.GetProperty("component").GetString().Should().Be("HookBridge.ControlPlane");
    }

    [Fact]
    public async Task ReadinessCheck_ShouldReturnOk_WithReadyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("status").GetString().Should().Be("Ready");
    }
}
