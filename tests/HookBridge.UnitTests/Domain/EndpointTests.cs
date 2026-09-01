using FluentAssertions;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;

namespace HookBridge.UnitTests.Domain;

public class EndpointTests
{
    [Fact]
    public void Create_WithValidHttpsUrl_ShouldSucceed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = Endpoint.Create(tenantId, appId, "https://api.acme.com/webhooks", "Test Endpoint", now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TargetUrl.Should().Be("https://api.acme.com/webhooks");
        result.Value.Status.Should().Be(EndpointStatus.Active);
        result.Value.TimeoutSeconds.Should().Be(10);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid-scheme.com")]
    [InlineData("")]
    public void Create_WithInvalidUrl_ShouldFail(string invalidUrl)
    {
        // Act
        var result = Endpoint.Create(Guid.NewGuid(), Guid.NewGuid(), invalidUrl, null, DateTimeOffset.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Endpoint.InvalidUrl");
    }

    [Fact]
    public void Create_WithExcessiveTimeout_ShouldClampToMax30Seconds()
    {
        // Act
        var result = Endpoint.Create(Guid.NewGuid(), Guid.NewGuid(), "https://api.acme.com/webhooks", null, DateTimeOffset.UtcNow, timeoutSeconds: 120);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TimeoutSeconds.Should().Be(30);
    }
}
