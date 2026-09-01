using FluentAssertions;
using HookBridge.Infrastructure.MultiTenancy;

namespace HookBridge.UnitTests.Infrastructure;

public class TenantContextTests
{
    [Fact]
    public void SetTenant_WithValidGuid_ShouldPopulateProperties()
    {
        // Arrange
        var context = new TenantContext();
        var tenantId = Guid.NewGuid();

        // Act
        context.SetTenant(tenantId, "slug-corp");

        // Assert
        context.TenantId.Should().Be(tenantId);
        context.TenantIdentifier.Should().Be("slug-corp");
        context.HasTenant.Should().BeTrue();
    }

    [Fact]
    public void InitialState_ShouldHaveNoTenant()
    {
        // Arrange & Act
        var context = new TenantContext();

        // Assert
        context.TenantId.Should().BeNull();
        context.HasTenant.Should().BeFalse();
    }
}
