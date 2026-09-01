using FluentAssertions;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;

namespace HookBridge.UnitTests.Domain;

public class TenantTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = Tenant.Create("acme-corp", "Acme Corporation", now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Identifier.Should().Be("acme-corp");
        result.Value.Name.Should().Be("Acme Corporation");
        result.Value.Status.Should().Be(TenantStatus.Active);
        result.Value.CreatedAt.Should().Be(now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyIdentifier_ShouldFail(string? identifier)
    {
        // Act
        var result = Tenant.Create(identifier!, "Acme Corp", DateTimeOffset.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.EmptyIdentifier");
    }
}
