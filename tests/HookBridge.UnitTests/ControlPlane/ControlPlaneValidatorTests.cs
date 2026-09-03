using FluentAssertions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.Validators;
using HookBridge.Domain.Enums;

namespace HookBridge.UnitTests.ControlPlane;

public class ControlPlaneValidatorTests
{
    [Fact]
    public void CreateApplicationValidator_ValidInput_ShouldPass()
    {
        var validator = new CreateApplicationValidator();
        var result = validator.Validate(new CreateApplicationCommand("Payment Service", "Handles payments"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateApplicationValidator_EmptyName_ShouldFail()
    {
        var validator = new CreateApplicationValidator();
        var result = validator.Validate(new CreateApplicationCommand("", "Description"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void CreateEndpointValidator_ValidInput_ShouldPass()
    {
        var validator = new CreateEndpointValidator();
        var result = validator.Validate(new CreateEndpointCommand(
            Guid.NewGuid(), "https://api.acme.com/webhooks", "Test Endpoint", 600, 10));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid-scheme.com")]
    [InlineData("")]
    public void CreateEndpointValidator_InvalidUrl_ShouldFail(string url)
    {
        var validator = new CreateEndpointValidator();
        var result = validator.Validate(new CreateEndpointCommand(Guid.NewGuid(), url, null, 600, 10));
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("order.*")]
    [InlineData("payment.settled.v1")]
    [InlineData("invoice.created")]
    public void CreateSubscriptionValidator_ValidPatterns_ShouldPass(string pattern)
    {
        var validator = new CreateSubscriptionValidator();
        var result = validator.Validate(new CreateSubscriptionCommand(pattern));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid pattern with spaces")]
    [InlineData("order..created")]
    public void CreateSubscriptionValidator_InvalidPatterns_ShouldFail(string pattern)
    {
        var validator = new CreateSubscriptionValidator();
        var result = validator.Validate(new CreateSubscriptionCommand(pattern));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateApiKeyValidator_ValidInput_ShouldPass()
    {
        var validator = new CreateApiKeyValidator();
        var result = validator.Validate(new CreateApiKeyCommand("CI Ingestion Key", ApiKeyScope.EventsIngest, "live"));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid_env")]
    [InlineData("staging")]
    public void CreateApiKeyValidator_InvalidEnvironment_ShouldFail(string env)
    {
        var validator = new CreateApiKeyValidator();
        var result = validator.Validate(new CreateApiKeyCommand("Test Key", ApiKeyScope.EventsIngest, env));
        result.IsValid.Should().BeFalse();
    }
}
