using FluentAssertions;
using HookBridge.Infrastructure.Security;

namespace HookBridge.UnitTests.Security;

public class KeyGeneratorTests
{
    private readonly KeyGenerator _generator = new();

    [Fact]
    public void GenerateApiKey_LiveEnvironment_ShouldStartWithHbLivePrefix()
    {
        // Act
        var (plainKey, keyPrefix, keyHash) = _generator.GenerateApiKey("live");

        // Assert
        plainKey.Should().StartWith("hb_live_");
        keyPrefix.Should().Be(plainKey[..16]);
        keyHash.Should().NotBeNullOrWhiteSpace();
        keyHash.Length.Should().Be(64); // SHA-256 in hex
    }

    [Fact]
    public void GenerateApiKey_TestEnvironment_ShouldStartWithHbTestPrefix()
    {
        // Act
        var (plainKey, keyPrefix, keyHash) = _generator.GenerateApiKey("test");

        // Assert
        plainKey.Should().StartWith("hb_test_");
        keyPrefix.Should().Be(plainKey[..16]);
    }

    [Fact]
    public void GenerateWebhookSecret_ShouldStartWithWhsecPrefix()
    {
        // Act
        var (plainSecret, secretPrefix, secretHash) = _generator.GenerateWebhookSecret();

        // Assert
        plainSecret.Should().StartWith("whsec_");
        secretPrefix.Should().Be(plainSecret[..14]);
        secretHash.Should().NotBeNullOrWhiteSpace();
        secretHash.Length.Should().Be(64);
    }

    [Fact]
    public void ComputeHash_ShouldBeDeterministic()
    {
        // Arrange
        var input = "test-input-string";

        // Act
        var hash1 = _generator.ComputeHash(input);
        var hash2 = _generator.ComputeHash(input);

        // Assert
        hash1.Should().Be(hash2);
    }
}
