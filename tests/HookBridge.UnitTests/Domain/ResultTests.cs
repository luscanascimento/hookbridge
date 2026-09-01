using FluentAssertions;
using HookBridge.Domain.Common;

namespace HookBridge.UnitTests.Domain;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(DomainError.None);
    }

    [Fact]
    public void Failure_ShouldCreateFailingResult_WithErrorDetails()
    {
        // Arrange
        var error = DomainError.Validation("Test.Code", "Validation failed.");

        // Act
        var result = Result.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void GenericSuccess_ShouldContainValue()
    {
        // Act
        var result = Result.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void GenericFailure_AccessingValue_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var error = DomainError.NotFound("Item.NotFound", "Item not found.");
        var result = Result.Failure<string>(error);

        // Act & Assert
        result.Invoking(r => _ = r.Value)
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*failed result*");
    }

    [Fact]
    public void Match_ShouldExecuteCorrectBranch()
    {
        // Arrange
        Result<string> successResult = "hello";
        Result<string> failureResult = DomainError.Conflict("Conflict", "Conflict occurred");

        // Act
        var successOutput = successResult.Match(v => $"Success: {v}", e => $"Error: {e.Message}");
        var failureOutput = failureResult.Match(v => $"Success: {v}", e => $"Error: {e.Message}");

        // Assert
        successOutput.Should().Be("Success: hello");
        failureOutput.Should().Be("Error: Conflict occurred");
    }
}
