using FluentAssertions;
using HookBridge.Domain.Entities;

namespace HookBridge.UnitTests.Domain;

public class RefreshTokenTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldBeActive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = RefreshToken.Create(userId, tenantId, "hash123", now, TimeSpan.FromDays(7));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.IsActive.Should().BeTrue();
        result.Value.ExpiresAt.Should().Be(now.AddDays(7));
    }

    [Fact]
    public void Revoke_ShouldMarkTokenAsInactive()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var token = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash123", now, TimeSpan.FromDays(7)).Value;

        // Act
        token.Revoke(now.AddHours(1), "newHash456");

        // Assert
        token.IsActive.Should().BeFalse();
        token.RevokedAt.Should().Be(now.AddHours(1));
        token.ReplacedByTokenHash.Should().Be("newHash456");
    }
}
