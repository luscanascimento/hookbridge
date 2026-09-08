using System.Net;
using FluentAssertions;
using HookBridge.Application.Common.Security;
using HookBridge.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.Security;

public class AdversarialSecurityTests
{
    private readonly SsrfGuard _ssrfGuard;
    private readonly WebhookSigner _webhookSigner;

    public AdversarialSecurityTests()
    {
        var ssrfOptions = Options.Create(new SsrfOptions
        {
            Enabled = true,
            ResolveDns = false, // Disable DNS in isolated unit tests to test static rules
            AllowedHosts = new List<string> { "allowed-internal-mock.net" }
        });

        _ssrfGuard = new SsrfGuard(ssrfOptions, NullLogger<SsrfGuard>.Instance);
        _webhookSigner = new WebhookSigner();
    }

    #region SSRF Adversarial Tests

    [Theory]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://localhost")]
    [InlineData("http://test.localhost")]
    [InlineData("http://service.local")]
    [InlineData("http://database.internal")]
    [InlineData("http://app.cluster.local")]
    [InlineData("http://host.docker.internal")]
    [InlineData("http://gateway.docker.internal")]
    [InlineData("http://kubernetes.default.svc")]
    [InlineData("http://169.254.169.254")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://169.254.170.2/v2/metadata")]
    [InlineData("http://metadata.google.internal")]
    [InlineData("http://100.100.100.200")]
    [InlineData("http://192.0.0.192")]
    [InlineData("http://10.0.0.1")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://0.0.0.0")]
    [InlineData("http://[::1]")]
    public async Task SsrfGuard_ShouldBlockProhibitedHostAndIpTargets(string url)
    {
        // Act
        var result = await _ssrfGuard.ValidateUrlAsync(url);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://admin:secret@api.example.com/webhook")]
    [InlineData("https://user:pass@hooks.domain.com")]
    public async Task SsrfGuard_ShouldBlockEmbeddedCredentials(string url)
    {
        // Act
        var result = await _ssrfGuard.ValidateUrlAsync(url);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ssrf.UserInfoProhibited");
    }

    [Theory]
    [InlineData("http://api.example.com:22/webhook")]    // SSH
    [InlineData("http://api.example.com:25/webhook")]    // SMTP
    [InlineData("http://api.example.com:3306/webhook")]  // MySQL
    [InlineData("http://api.example.com:5432/webhook")]  // PostgreSQL
    [InlineData("http://api.example.com:6379/webhook")]  // Redis
    [InlineData("http://api.example.com:27017/webhook")] // MongoDB
    [InlineData("http://api.example.com:6443/webhook")]  // K8s API
    [InlineData("http://api.example.com:9200/webhook")]  // Elasticsearch
    public async Task SsrfGuard_ShouldBlockDangerousPorts(string url)
    {
        // Act
        var result = await _ssrfGuard.ValidateUrlAsync(url);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ssrf.InvalidPort");
    }

    [Theory]
    [InlineData("ftp://api.example.com/webhook")]
    [InlineData("gopher://api.example.com/webhook")]
    [InlineData("file:///etc/passwd")]
    [InlineData("dict://api.example.com/webhook")]
    public async Task SsrfGuard_ShouldBlockNonHttpProtocols(string url)
    {
        // Act
        var result = await _ssrfGuard.ValidateUrlAsync(url);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ssrf.InvalidScheme");
    }

    [Theory]
    [InlineData("https://api.stripe.com/v1/webhooks")]
    [InlineData("https://hooks.slack.com/services/T00/B00/X00")]
    [InlineData("https://api.merchant.com/callbacks/order-completed")]
    public async Task SsrfGuard_ShouldAllowLegitimatePublicEndpoints(string url)
    {
        // Act
        var result = await _ssrfGuard.ValidateUrlAsync(url);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SsrfGuard_ShouldAllowWhitelistedInternalHost()
    {
        // Act
        var result = await _ssrfGuard.ValidateUrlAsync("https://allowed-internal-mock.net/webhook");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Webhook HMAC Anti-Replay & Spoofing Tests

    [Fact]
    public void WebhookSigner_ShouldVerifyValidSignature()
    {
        // Arrange
        var secret = "whsec_test_secret_key_123456789";
        var payload = "{\"event\": \"order.paid\", \"amount\": 100}";
        var now = DateTimeOffset.UtcNow;
        var header = _webhookSigner.GenerateSignatureHeader(payload, secret, now);

        // Act
        var result = _webhookSigner.VerifySignature(payload, header, secret, tolerance: TimeSpan.FromMinutes(5), now: now);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void WebhookSigner_WhenTimestampExpired_ShouldRejectReplayAttack()
    {
        // Arrange
        var secret = "whsec_test_secret_key_123456789";
        var payload = "{\"event\": \"order.paid\", \"amount\": 100}";
        var now = DateTimeOffset.UtcNow;
        var expiredTime = now.AddMinutes(-6); // 6 mins ago (tolerance is 5 mins)
        var header = _webhookSigner.GenerateSignatureHeader(payload, secret, expiredTime);

        // Act
        var result = _webhookSigner.VerifySignature(payload, header, secret, tolerance: TimeSpan.FromMinutes(5), now: now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Signature.TimestampOutOfTolerance");
    }

    [Fact]
    public void WebhookSigner_WhenTimestampTooFarInFuture_ShouldRejectFutureSpoofing()
    {
        // Arrange
        var secret = "whsec_test_secret_key_123456789";
        var payload = "{\"event\": \"order.paid\", \"amount\": 100}";
        var now = DateTimeOffset.UtcNow;
        var futureTime = now.AddMinutes(5); // 5 mins in future (> 60s future drift)
        var header = _webhookSigner.GenerateSignatureHeader(payload, secret, futureTime);

        // Act
        var result = _webhookSigner.VerifySignature(payload, header, secret, tolerance: TimeSpan.FromMinutes(5), now: now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Signature.FutureTimestamp");
    }

    [Fact]
    public void WebhookSigner_WhenPayloadTampered_ShouldRejectForgedSignature()
    {
        // Arrange
        var secret = "whsec_test_secret_key_123456789";
        var originalPayload = "{\"event\": \"order.paid\", \"amount\": 100}";
        var tamperedPayload = "{\"event\": \"order.paid\", \"amount\": 999999}";
        var now = DateTimeOffset.UtcNow;
        var header = _webhookSigner.GenerateSignatureHeader(originalPayload, secret, now);

        // Act
        var result = _webhookSigner.VerifySignature(tamperedPayload, header, secret, tolerance: TimeSpan.FromMinutes(5), now: now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Signature.Invalid");
    }

    [Fact]
    public void WebhookSigner_DualSecretRotation_ShouldAcceptBothOldAndNewSecrets()
    {
        // Arrange
        var oldSecret = "whsec_old_key_1111111111111";
        var newSecret = "whsec_new_key_2222222222222";
        var payload = "{\"event\": \"customer.created\"}";
        var now = DateTimeOffset.UtcNow;

        // Header generated with old secret
        var headerOld = _webhookSigner.GenerateSignatureHeader(payload, oldSecret, now);
        // Header generated with new secret
        var headerNew = _webhookSigner.GenerateSignatureHeader(payload, newSecret, now);

        var candidateSecrets = new[] { oldSecret, newSecret };

        // Act
        var verifyOldResult = _webhookSigner.VerifySignature(payload, headerOld, candidateSecrets, tolerance: TimeSpan.FromMinutes(5), now: now);
        var verifyNewResult = _webhookSigner.VerifySignature(payload, headerNew, candidateSecrets, tolerance: TimeSpan.FromMinutes(5), now: now);

        // Assert
        verifyOldResult.IsSuccess.Should().BeTrue();
        verifyNewResult.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Input Sanitization & XSS Neutralization Tests

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<SCRIPT SRC=http://evil.com/xss.js></SCRIPT>")]
    [InlineData("<img src=x onerror=alert('xss')>")]
    [InlineData("<body onload=alert('xss')>")]
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("<iframe src=\"javascript:alert('xss')\"></iframe>")]
    public void InputSanitizer_ShouldDetectXssVectors(string maliciousInput)
    {
        // Act & Assert
        InputSanitizer.ContainsXssVectors(maliciousInput).Should().BeTrue();
    }

    [Theory]
    [InlineData("payment.completed")]
    [InlineData("Order #12345 received successfully.")]
    [InlineData("https://api.acme.com/v1/webhook")]
    [InlineData("Valid description with standard punctuation! (v1.0)")]
    public void InputSanitizer_ShouldPassSafeInputs(string cleanInput)
    {
        // Act & Assert
        InputSanitizer.ContainsXssVectors(cleanInput).Should().BeFalse();
    }

    [Theory]
    [InlineData("valid-slug-123", true)]
    [InlineData("orders-paid", true)]
    [InlineData("a", true)]
    [InlineData("../path/traversal", false)]
    [InlineData("slug with spaces", false)]
    [InlineData("slug_with_underscore", false)]
    [InlineData("slug<script>", false)]
    [InlineData("", false)]
    public void InputSanitizer_ShouldValidateSlugs(string slug, bool expectedValid)
    {
        // Act & Assert
        InputSanitizer.IsValidSlug(slug).Should().Be(expectedValid);
    }

    #endregion
}
