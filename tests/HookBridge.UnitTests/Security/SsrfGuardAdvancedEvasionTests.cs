using System.Net;
using FluentAssertions;
using HookBridge.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.Security;

public sealed class SsrfGuardAdvancedEvasionTests
{
    private readonly SsrfGuard _guard;

    public SsrfGuardAdvancedEvasionTests()
    {
        var options = new SsrfOptions
        {
            Enabled = true,
            ResolveDns = false, // Unit tests test host/IP parser without DNS dependency
            AllowedHosts = ["safe.external.com", "webhook.trusted.org"]
        };

        _guard = new SsrfGuard(Options.Create(options), NullLogger<SsrfGuard>.Instance);
    }

    [Theory]
    [InlineData("http://2130706433/webhook")]          // 127.0.0.1 as decimal integer
    [InlineData("https://2852039166/metadata")]        // 169.254.169.254 (IMDS) as decimal integer
    [InlineData("http://0x7f000001/hook")]             // 127.0.0.1 as hex integer
    [InlineData("https://0xa9fea9fe/imds")]            // 169.254.169.254 as hex integer
    [InlineData("http://0177.0.0.1/notify")]           // 127.0.0.1 with octal prefix
    [InlineData("https://0x7f.0.0.1/notify")]          // 127.0.0.1 with hex dotted segment
    [InlineData("http://127.0.0.1:5432/db")]           // Prohibited database port (PostgreSQL)
    [InlineData("http://127.0.0.1:6379/redis")]        // Prohibited cache port (Redis)
    [InlineData("http://127.0.0.1:22/ssh")]            // Prohibited port (SSH)
    [InlineData("http://user:secret@safe.external.com")] // UserInfo embedded credentials
    public async Task ValidateUrlAsync_WhenEvasionPatternUsed_ShouldBlockWithValidationError(string attackUrl)
    {
        // Act
        var result = await _guard.ValidateUrlAsync(attackUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(HookBridge.Domain.Common.ErrorType.Validation);
    }

    [Theory]
    [InlineData("https://safe.external.com/webhook")]
    [InlineData("https://webhook.trusted.org/events")]
    public async Task ValidateUrlAsync_WhenAllowedHostUsed_ShouldPassValidation(string allowedUrl)
    {
        // Act
        var result = await _guard.ValidateUrlAsync(allowedUrl);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRedirectUrlAsync_WhenRedirectTargetsMetadataOrLoopback_ShouldBlockRedirect()
    {
        // Arrange
        const string currentUrl = "https://safe.external.com/outbound";

        // Act
        var loopbackRedirect = await _guard.ValidateRedirectUrlAsync(currentUrl, "http://127.0.0.1/secret");
        var metadataDecimalRedirect = await _guard.ValidateRedirectUrlAsync(currentUrl, "http://2852039166/latest/meta-data");
        var relativeLoopbackPortRedirect = await _guard.ValidateRedirectUrlAsync(currentUrl, "http://127.0.0.1:5432/");

        // Assert
        loopbackRedirect.IsFailure.Should().BeTrue();
        metadataDecimalRedirect.IsFailure.Should().BeTrue();
        relativeLoopbackPortRedirect.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRedirectUrlAsync_WhenRedirectTargetsSafeRelativeOrAllowedUrl_ShouldSucceed()
    {
        // Arrange
        const string currentUrl = "https://safe.external.com/hook";

        // Act
        var relativeRedirect = await _guard.ValidateRedirectUrlAsync(currentUrl, "/v2/hook");
        var absoluteAllowedRedirect = await _guard.ValidateRedirectUrlAsync(currentUrl, "https://webhook.trusted.org/v2");

        // Assert
        relativeRedirect.IsSuccess.Should().BeTrue();
        absoluteAllowedRedirect.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CreateSafeSocketsHttpHandler_ShouldConfigureSafeRedirectAndConnectCallback()
    {
        // Act
        var handler = _guard.CreateSafeSocketsHttpHandler();

        // Assert
        handler.Should().NotBeNull();
        handler.AllowAutoRedirect.Should().BeFalse();
        handler.ConnectCallback.Should().NotBeNull();
    }

    [Theory]
    [InlineData("2130706433", "127.0.0.1")]
    [InlineData("2852039166", "169.254.169.254")]
    [InlineData("0x7f000001", "127.0.0.1")]
    [InlineData("0177.0.0.1", "127.0.0.1")]
    [InlineData("0x7f.0.0.1", "127.0.0.1")]
    public void TryNormalizeAlternativeIp_ShouldDecodeCorrectly(string inputHost, string expectedIp)
    {
        // Act
        var success = SsrfGuard.TryNormalizeAlternativeIp(inputHost, out var normalizedIp);

        // Assert
        success.Should().BeTrue();
        normalizedIp.Should().NotBeNull();
        normalizedIp!.ToString().Should().Be(expectedIp);
    }
}
