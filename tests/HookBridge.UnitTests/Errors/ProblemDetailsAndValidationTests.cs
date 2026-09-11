using FluentAssertions;
using FluentValidation.Results;
using HookBridge.Api.Common;
using HookBridge.Application.Common;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.UnitTests.Errors;

public class ProblemDetailsAndValidationTests
{
    [Fact]
    public void ValidationResultExtensions_ToDomainError_ShouldMapAllFieldErrors()
    {
        // Arrange
        var failures = new List<ValidationFailure>
        {
            new("TargetUrl", "A valid HTTP/HTTPS URL is required."),
            new("TargetUrl", "Cannot exceed 2048 characters."),
            new("RateLimitPerMinute", "Rate limit must be greater than 0.")
        };
        var validationResult = new ValidationResult(failures);

        // Act
        var domainError = validationResult.ToDomainError();

        // Assert
        domainError.Type.Should().Be(ErrorType.Validation);
        domainError.Code.Should().Be("TargetUrl");
        domainError.Message.Should().Be("A valid HTTP/HTTPS URL is required.");
        domainError.ValidationErrors.Should().NotBeNull();
        domainError.ValidationErrors!.Should().ContainKey("TargetUrl");
        domainError.ValidationErrors["TargetUrl"].Should().HaveCount(2);
        domainError.ValidationErrors!.Should().ContainKey("RateLimitPerMinute");
        domainError.ValidationErrors["RateLimitPerMinute"].Should().ContainSingle();
    }

    [Fact]
    public void HttpResults_ToProblem_Validation_ShouldProduceRFC7807ProblemDetails()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            ["Email"] = new[] { "Email is invalid." }
        };
        var domainError = DomainError.Validation("Email.Invalid", "The email format is invalid.", errors);

        // Act
        var result = HttpResults.ToProblem(domainError);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.StatusCode.Should().Be(400);
        problem.ProblemDetails.Title.Should().Be("Validation Failure");
        problem.ProblemDetails.Detail.Should().Be("The email format is invalid.");
        problem.ProblemDetails.Extensions.Should().ContainKey("errorCode");
        problem.ProblemDetails.Extensions.Should().ContainKey("errors");
    }

    [Theory]
    [InlineData(ErrorType.NotFound, 404, "Not Found")]
    [InlineData(ErrorType.Conflict, 409, "Conflict")]
    [InlineData(ErrorType.Unauthorized, 401, "Unauthorized")]
    [InlineData(ErrorType.Forbidden, 403, "Forbidden")]
    [InlineData(ErrorType.Validation, 400, "Validation Failure")]
    [InlineData(ErrorType.Failure, 500, "Internal Failure")]
    public void DomainError_AllTypes_ShouldMapToCorrectHttpStatusCodes(ErrorType errorType, int expectedStatusCode, string expectedTitle)
    {
        // Arrange
        var domainError = new DomainError("Test.Code", "Test message", errorType);

        // Act
        var result = HttpResults.ToProblem(domainError);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problemHttpResult = (ProblemHttpResult)result;
        problemHttpResult.StatusCode.Should().Be(expectedStatusCode);
        problemHttpResult.ProblemDetails.Title.Should().Be(expectedTitle);
    }
}
