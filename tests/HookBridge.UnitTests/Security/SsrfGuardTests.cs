using FluentAssertions;
using HookBridge.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.Security;

public class SsrfGuardTests
{
    private readonly SsrfGuard _guard;

    public SsrfGuardTests()
    {
        var options = Options.Create(new SsrfOptions
        {
            Enabled = true,
            ResolveDns = false // Disable DNS for pure IP unit tests
        });
        _guard = new SsrfGuard(options, NullLogger<SsrfGuard>.Instance);
    }

    [Theory]
    [InlineData("http://127.0.0.1/webhook")]
    [InlineData("http://127.0.0.2:8080/webhook")]
    [InlineData("http://localhost:3000/webhook")]
    [InlineData("http://app.localhost/webhook")]
    [InlineData("http://10.0.0.1/endpoint")]
    [InlineData("http://172.16.5.10/webhook")]
    [InlineData("http://192.168.1.100/webhook")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://metadata.google.internal/computeMetadata/v1/")]
    [InlineData("http://[::1]/webhook")]
    [InlineData("http://[fc00::1]/webhook")]
    public async Task ValidateUrl_ProhibitedDestinations_ShouldReturnFailure(string url)
    {
        // Act
        var result = await _guard.ValidateUrlAsync(url);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://api.github.com/webhooks")]
    [InlineData("https://hooks.slack.com/services/T00/B00/X00")]
    [InlineData("https://example.com/api/v1/webhook")]
    [InlineData("http://93.184.216.34/webhook")]
    public async Task ValidateUrl_AllowedDestinations_ShouldReturnSuccess(string url)
    {
        // Act
        var result = await _guard.ValidateUrlAsync(url);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("ftp://api.github.com/webhook")]
    [InlineData("gopher://example.com")]
    [InlineData("file:///etc/passwd")]
    public async Task ValidateUrl_DisallowedSchemes_ShouldReturnFailure(string url)
    {
        // Act
        var result = await _guard.ValidateUrlAsync(url);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ssrf.InvalidScheme");
    }
}
