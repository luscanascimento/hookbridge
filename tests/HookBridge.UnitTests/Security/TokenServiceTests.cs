using FluentAssertions;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.Security;

public class TokenServiceTests
{
    private readonly TokenService _tokenService;

    public TokenServiceTests()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "HookBridge.Test",
            Audience = "HookBridge.TestAudience",
            SecretKey = "HookBridge_Test_Key_Must_Be_Long_Enough_For_HmacSha256_Signature!",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        });

        _tokenService = new TokenService(options);
    }

    [Fact]
    public void GenerateTokens_ShouldReturnValidTokens()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("acme-test", "Acme Test", now).Value;
        var user = User.Create(tenant.Id, "admin@acme.test", "dummy_hash", UserRole.TenantAdmin, now).Value;

        // Act
        var tokens = _tokenService.GenerateTokens(user, tenant);

        // Assert
        tokens.Should().NotBeNull();
        tokens.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshTokenHash.Should().NotBeNullOrWhiteSpace();
        tokens.ExpiresInSeconds.Should().Be(15 * 60);
    }

    [Fact]
    public void HashToken_ShouldProduceDeterministicSha256Hash()
    {
        // Arrange
        const string rawToken = "raw-refresh-token-xyz-123";

        // Act
        string hash1 = _tokenService.HashToken(rawToken);
        string hash2 = _tokenService.HashToken(rawToken);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64); // SHA-256 hex string length
    }
}
