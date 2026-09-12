using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HookBridge.UnitTests.Security;

public sealed class JwtHardeningAndAlgorithmValidationTests
{
    private const string StrongKey32 = "Super_Secret_Test_Key_At_Least_32_Bytes_Long_2026!";

    [Theory]
    [InlineData("")]
    [InlineData("short_key")]
    [InlineData("key_less_than_32_chars_1234567")]
    public void TokenService_WhenSecretKeyIsWeakOrEmpty_ShouldThrowInvalidOperationException(string weakKey)
    {
        // Arrange
        var options = Options.Create(new JwtOptions
        {
            SecretKey = weakKey,
            Issuer = "TestIssuer",
            Audience = "TestAudience"
        });

        // Act
        Action act = () => _ = new TokenService(options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 32 characters (256 bits)*");
    }

    [Fact]
    public void GenerateTokens_ShouldProduceValidHs256Token_WithStandardClaims()
    {
        // Arrange
        var options = Options.Create(new JwtOptions
        {
            SecretKey = StrongKey32,
            Issuer = "HookBridge.ControlPlane",
            Audience = "HookBridge.DeveloperPortal",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        });

        var tokenService = new TokenService(options);
        var tenantId = Guid.NewGuid();
        var tenant = Tenant.Create("acme-corp", "Acme Corp", DateTimeOffset.UtcNow).Value;
        typeof(Tenant).GetProperty("Id")!.SetValue(tenant, tenantId);

        var user = User.Create(tenantId, "admin@acme.com", "hash", UserRole.TenantAdmin, DateTimeOffset.UtcNow).Value;

        // Act
        var tokens = tokenService.GenerateTokens(user, tenant);

        // Assert
        tokens.Should().NotBeNull();
        tokens.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshTokenHash.Should().NotBeNullOrWhiteSpace();
        tokens.ExpiresInSeconds.Should().Be(15 * 60);

        // Validate token structure with JwtSecurityTokenHandler
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokens.AccessToken);

        jwt.Header.Alg.Should().Be(SecurityAlgorithms.HmacSha256);
        jwt.Issuer.Should().Be("HookBridge.ControlPlane");
        jwt.Audiences.Should().Contain("HookBridge.DeveloperPortal");

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "admin@acme.com");
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == "TenantAdmin");
        jwt.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "tenant_slug" && c.Value == "acme-corp");
    }

    [Fact]
    public void TokenValidation_WhenAlgorithmIsNone_ShouldRejectToken()
    {
        // Arrange: Build token without signature / 'none' algorithm
        var header = new JwtHeader();
        header["alg"] = "none";

        var payload = new JwtPayload
        {
            { "sub", Guid.NewGuid().ToString() },
            { "iss", "HookBridge.ControlPlane" },
            { "aud", "HookBridge.DeveloperPortal" },
            { "exp", DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds() }
        };

        var unsignedToken = new JwtSecurityToken(header, payload);
        var tokenString = new JwtSecurityTokenHandler().WriteToken(unsignedToken);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "HookBridge.ControlPlane",
            ValidateAudience = true,
            ValidAudience = "HookBridge.DeveloperPortal",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(StrongKey32)),
            ValidateLifetime = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };

        var handler = new JwtSecurityTokenHandler();

        // Act
        Action validateAct = () => handler.ValidateToken(tokenString, validationParameters, out _);

        // Assert
        validateAct.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void TokenValidation_WhenSignedWithWrongKey_ShouldFailValidation()
    {
        // Arrange
        const string wrongKey = "Different_Key_That_Is_Also_At_Least_32_Bytes_Long!";
        var options = Options.Create(new JwtOptions
        {
            SecretKey = StrongKey32,
            Issuer = "HookBridge.ControlPlane",
            Audience = "HookBridge.DeveloperPortal"
        });

        var tokenService = new TokenService(options);
        var tenant = Tenant.Create("acme", "Acme", DateTimeOffset.UtcNow).Value;
        var user = User.Create(tenant.Id, "user@acme.com", "hash", UserRole.Developer, DateTimeOffset.UtcNow).Value;
        var tokens = tokenService.GenerateTokens(user, tenant);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "HookBridge.ControlPlane",
            ValidateAudience = true,
            ValidAudience = "HookBridge.DeveloperPortal",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(wrongKey)),
            ValidateLifetime = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };

        var handler = new JwtSecurityTokenHandler();

        // Act
        Action validateAct = () => handler.ValidateToken(tokens.AccessToken, validationParameters, out _);

        // Assert
        validateAct.Should().Throw<SecurityTokenSignatureKeyNotFoundException>();
    }
}
