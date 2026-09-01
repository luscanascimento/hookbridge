using System.Net;
using FluentAssertions;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.Middleware;

public class TenantResolutionMiddlewareTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TenantResolutionMiddlewareTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Request_WithValidTenantHeader_ShouldSucceed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Tenant-ID", tenantId.ToString());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
