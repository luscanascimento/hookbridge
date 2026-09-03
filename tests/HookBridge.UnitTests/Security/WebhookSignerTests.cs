using FluentAssertions;
using HookBridge.Infrastructure.Security;

namespace HookBridge.UnitTests.Security;

public class WebhookSignerTests
{
    private readonly WebhookSigner _signer = new();

    [Fact]
    public void GenerateSignatureHeader_SingleSecret_ShouldProduceStandardFormat()
    {
        // Arrange
        var payload = "{\"event\":\"order.created\",\"id\":\"ord_123\"}";
        var secret = "whsec_test_secret_key_1234567890abcdef";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var header = _signer.GenerateSignatureHeader(payload, secret, timestamp);

        // Assert
        header.Should().StartWith($"t={timestamp.ToUnixTimeSeconds()},v1=");
        var parts = header.Split(',');
        parts.Should().HaveCount(2);
        parts[0].Should().Be($"t={timestamp.ToUnixTimeSeconds()}");
        parts[1].Should().StartWith("v1=");
        parts[1][3..].Length.Should().Be(64); // SHA-256 in hex
    }

    [Fact]
    public void GenerateSignatureHeader_MultipleSecrets_ShouldEmitDualSignatures()
    {
        // Arrange
        var payload = "{\"event\":\"payment.settled\"}";
        var secret1 = "whsec_active_key_1111";
        var secret2 = "whsec_rotating_key_2222";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var header = _signer.GenerateSignatureHeader(payload, new[] { secret1, secret2 }, timestamp);

        // Assert
        var parts = header.Split(',');
        parts.Should().HaveCount(3);
        parts[0].Should().Be($"t={timestamp.ToUnixTimeSeconds()}");
        parts[1].Should().StartWith("v1=");
        parts[2].Should().StartWith("v1=");
        parts[1].Should().NotBe(parts[2]);
    }

    [Fact]
    public void VerifySignature_WithValidSignatureAndPayload_ShouldSucceed()
    {
        // Arrange
        var payload = "{\"event\":\"invoice.paid\",\"amount\":5000}";
        var secret = "whsec_valid_key_3333";
        var now = DateTimeOffset.UtcNow;
        var header = _signer.GenerateSignatureHeader(payload, secret, now);

        // Act
        var result = _signer.VerifySignature(payload, header, secret, TimeSpan.FromMinutes(5), now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_DualSecretRotation_ShouldSucceedWithRotatingKey()
    {
        // Arrange
        var payload = "{\"event\":\"invoice.paid\"}";
        var activeSecret = "whsec_new_active_key";
        var rotatingSecret = "whsec_old_rotating_key";
        var now = DateTimeOffset.UtcNow;

        // Header generated with dual secrets
        var header = _signer.GenerateSignatureHeader(payload, new[] { activeSecret, rotatingSecret }, now);

        // Client verifies with old rotating secret
        var result = _signer.VerifySignature(payload, header, rotatingSecret, TimeSpan.FromMinutes(5), now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_WithTamperedPayload_ShouldFail()
    {
        // Arrange
        var originalPayload = "{\"event\":\"payment.settled\",\"amount\":100}";
        var tamperedPayload = "{\"event\":\"payment.settled\",\"amount\":100000}";
        var secret = "whsec_secret_key_4444";
        var now = DateTimeOffset.UtcNow;

        var header = _signer.GenerateSignatureHeader(originalPayload, secret, now);

        // Act
        var result = _signer.VerifySignature(tamperedPayload, header, secret, TimeSpan.FromMinutes(5), now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Signature.Invalid");
    }

    [Fact]
    public void VerifySignature_WithExpiredTimestamp_ShouldFailAntiReplayCheck()
    {
        // Arrange
        var payload = "{\"event\":\"test\"}";
        var secret = "whsec_secret_key_5555";
        var now = DateTimeOffset.UtcNow;
        var pastTimestamp = now.AddMinutes(-10); // 10 minutes ago (> 300s)

        var header = _signer.GenerateSignatureHeader(payload, secret, pastTimestamp);

        // Act
        var result = _signer.VerifySignature(payload, header, secret, TimeSpan.FromSeconds(300), now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Signature.TimestampOutOfTolerance");
    }

    [Theory]
    [InlineData("invalid_header")]
    [InlineData("t=notanumber,v1=123")]
    [InlineData("t=1700000000")]
    [InlineData("v1=abcd1234")]
    public void VerifySignature_WithMalformedHeader_ShouldFail(string malformedHeader)
    {
        // Act
        var result = _signer.VerifySignature("payload", malformedHeader, "whsec_secret", TimeSpan.FromMinutes(5));

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
