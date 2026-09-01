using FluentAssertions;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.Middleware;

public class SecurityHeadersTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Response_ShouldContainAllDefensiveSecurityHeaders()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.Headers.Contains("X-Content-Type-Options").Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");

        response.Headers.Contains("X-Frame-Options").Should().BeTrue();
        response.Headers.GetValues("X-Frame-Options").First().Should().Be("DENY");

        response.Headers.Contains("Referrer-Policy").Should().BeTrue();
        response.Headers.GetValues("Referrer-Policy").First().Should().Be("strict-origin-when-cross-origin");

        response.Headers.Contains("Content-Security-Policy").Should().BeTrue();
        response.Headers.GetValues("Content-Security-Policy").First().Should().Contain("default-src 'self'");
    }
}
