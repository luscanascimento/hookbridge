using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.Api;

public class DiagnosticsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DiagnosticsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDiagnosticsInfo_ShouldReturnOk_WithServiceMetadata()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/diagnostics/info");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("service").GetString().Should().Be("HookBridge");
        json.GetProperty("environment").GetString().Should().Be("Testing");
        json.GetProperty("processId").GetInt32().Should().BeGreaterThan(0);
    }
}
