using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.Auth.Validators;

namespace HookBridge.UnitTests.Application;

public class RegisterTenantValidatorTests
{
    private readonly RegisterTenantValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        // Arrange
        var command = new RegisterTenantCommand(
            TenantIdentifier: "enterprise-01",
            TenantName: "Enterprise Org",
            AdminEmail: "admin@enterprise.com",
            AdminPassword: "SecurePassword#2026");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Invalid Slug With Spaces")]
    [InlineData("slug_with_UPPERCASE")]
    [InlineData("a")]
    [InlineData("")]
    public void Validate_WithInvalidSlug_ShouldFail(string slug)
    {
        // Arrange
        var command = new RegisterTenantCommand(
            TenantIdentifier: slug,
            TenantName: "Enterprise Org",
            AdminEmail: "admin@enterprise.com",
            AdminPassword: "SecurePassword#2026");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterTenantCommand.TenantIdentifier));
    }

    [Theory]
    [InlineData("weak")]
    [InlineData("nocapital123!")]
    [InlineData("NOLOWERCASE123!")]
    [InlineData("NoSpecialChar123")]
    [InlineData("NoNumberHere!!")]
    public void Validate_WithWeakPassword_ShouldFail(string password)
    {
        // Arrange
        var command = new RegisterTenantCommand(
            TenantIdentifier: "valid-slug",
            TenantName: "Valid Name",
            AdminEmail: "admin@valid.com",
            AdminPassword: password);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterTenantCommand.AdminPassword));
    }
}
