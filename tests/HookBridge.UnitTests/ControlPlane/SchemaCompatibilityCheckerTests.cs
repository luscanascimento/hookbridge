using FluentAssertions;
using HookBridge.Application.ControlPlane.Services;
using HookBridge.Domain.Enums;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class SchemaCompatibilityCheckerTests
{
    private readonly SchemaCompatibilityChecker _checker = new();

    [Fact]
    public void CheckCompatibility_WithModeNone_ShouldAlwaysBeCompatible()
    {
        // Arrange
        var oldSchema = """{"type": "object", "properties": {"id": {"type": "string"}}}""";
        var newSchema = """{"type": "array"}""";

        // Act
        var result = _checker.CheckCompatibility(oldSchema, newSchema, SchemaCompatibilityMode.None);

        // Assert
        result.IsCompatible.Should().BeTrue();
        result.BreakingChanges.Should().BeEmpty();
    }

    [Fact]
    public void CheckCompatibility_WithInitialSchema_ShouldBeCompatible()
    {
        // Arrange
        var newSchema = """{"type": "object", "properties": {"id": {"type": "string"}}}""";

        // Act
        var result = _checker.CheckCompatibility(string.Empty, newSchema, SchemaCompatibilityMode.Backward);

        // Assert
        result.IsCompatible.Should().BeTrue();
    }

    [Fact]
    public void CheckCompatibility_Backward_AddingOptionalProperty_ShouldBeCompatible()
    {
        // Arrange
        var oldSchema = """
        {
            "type": "object",
            "required": ["id"],
            "properties": {
                "id": { "type": "string" }
            }
        }
        """;

        var newSchema = """
        {
            "type": "object",
            "required": ["id"],
            "properties": {
                "id": { "type": "string" },
                "description": { "type": "string" }
            }
        }
        """;

        // Act
        var result = _checker.CheckCompatibility(oldSchema, newSchema, SchemaCompatibilityMode.Backward);

        // Assert
        result.IsCompatible.Should().BeTrue();
        result.BreakingChanges.Should().BeEmpty();
        result.NonBreakingChanges.Should().Contain(n => n.Contains("description"));
    }

    [Fact]
    public void CheckCompatibility_Backward_AddingNewRequiredProperty_ShouldBeIncompatible()
    {
        // Arrange
        var oldSchema = """
        {
            "type": "object",
            "required": ["id"],
            "properties": {
                "id": { "type": "string" }
            }
        }
        """;

        var newSchema = """
        {
            "type": "object",
            "required": ["id", "newMandatoryField"],
            "properties": {
                "id": { "type": "string" },
                "newMandatoryField": { "type": "number" }
            }
        }
        """;

        // Act
        var result = _checker.CheckCompatibility(oldSchema, newSchema, SchemaCompatibilityMode.Backward);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().Contain(b => b.Contains("newMandatoryField") && b.Contains("New required property added"));
    }

    [Fact]
    public void CheckCompatibility_Backward_ChangingPropertyType_ShouldBeIncompatible()
    {
        // Arrange
        var oldSchema = """{"type": "object", "properties": {"amount": {"type": "number"}}}""";
        var newSchema = """{"type": "object", "properties": {"amount": {"type": "string"}}}""";

        // Act
        var result = _checker.CheckCompatibility(oldSchema, newSchema, SchemaCompatibilityMode.Backward);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().Contain(b => b.Contains("Type changed from 'number' to 'string'"));
    }

    [Fact]
    public void CheckCompatibility_Forward_RemovingRequiredProperty_ShouldBeIncompatible()
    {
        // Arrange
        var oldSchema = """
        {
            "type": "object",
            "required": ["orderId", "total"],
            "properties": {
                "orderId": { "type": "string" },
                "total": { "type": "number" }
            }
        }
        """;

        var newSchema = """
        {
            "type": "object",
            "required": ["orderId"],
            "properties": {
                "orderId": { "type": "string" }
            }
        }
        """;

        // Act
        var result = _checker.CheckCompatibility(oldSchema, newSchema, SchemaCompatibilityMode.Forward);

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().Contain(b => b.Contains("total") && b.Contains("Required property was removed"));
    }

    [Fact]
    public void CheckCompatibility_Full_RequiresBothBackwardAndForwardCompatibility()
    {
        // Arrange
        var oldSchema = """
        {
            "type": "object",
            "required": ["id"],
            "properties": {
                "id": { "type": "string" },
                "notes": { "type": "string" }
            }
        }
        """;

        // Adding optional is fine
        var compatibleNew = """
        {
            "type": "object",
            "required": ["id"],
            "properties": {
                "id": { "type": "string" },
                "notes": { "type": "string" },
                "tags": { "type": "array" }
            }
        }
        """;

        // Act
        var result = _checker.CheckCompatibility(oldSchema, compatibleNew, SchemaCompatibilityMode.Full);

        // Assert
        result.IsCompatible.Should().BeTrue();
    }
}
