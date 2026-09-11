using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using HookBridge.Infrastructure;
using HookBridge.Infrastructure.Integration;
using HookBridge.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.Configuration;

public sealed class ConfigurationAndSecretsValidationTests
{
    [Fact]
    public void JwtOptions_WithEmptySecretKey_ShouldFailValidation()
    {
        var options = new JwtOptions
        {
            Issuer = "HookBridge",
            Audience = "Clients",
            SecretKey = string.Empty
        };

        var validationResults = ValidateModel(options);

        validationResults.Should().Contain(v => v.MemberNames.Contains(nameof(JwtOptions.SecretKey)));
    }

    [Fact]
    public void JwtOptions_WithShortSecretKey_ShouldFailValidation()
    {
        var options = new JwtOptions
        {
            Issuer = "HookBridge",
            Audience = "Clients",
            SecretKey = "short_key_under_32_bytes"
        };

        var validationResults = ValidateModel(options);

        validationResults.Should().Contain(v => v.MemberNames.Contains(nameof(JwtOptions.SecretKey)));
    }

    [Fact]
    public void JwtOptions_WithValidConfiguration_ShouldPassValidation()
    {
        var options = new JwtOptions
        {
            Issuer = "HookBridge",
            Audience = "Clients",
            SecretKey = "valid_secure_jwt_secret_key_at_least_32_characters_long_2026!",
            AccessTokenExpirationMinutes = 30,
            RefreshTokenExpirationDays = 14
        };

        var validationResults = ValidateModel(options);

        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void WebhookEncryptionOptions_WithEmptyMasterKey_ShouldFailValidation()
    {
        var options = new WebhookEncryptionOptions
        {
            MasterKey = string.Empty
        };

        var validationResults = ValidateModel(options);

        validationResults.Should().Contain(v => v.MemberNames.Contains(nameof(WebhookEncryptionOptions.MasterKey)));
    }

    [Fact]
    public void WebhookEncryptionOptions_WithShortMasterKey_ShouldFailValidation()
    {
        var options = new WebhookEncryptionOptions
        {
            MasterKey = "too_short_key"
        };

        var validationResults = ValidateModel(options);

        validationResults.Should().Contain(v => v.MemberNames.Contains(nameof(WebhookEncryptionOptions.MasterKey)));
    }

    [Fact]
    public void WebhookEncryptionOptions_WithValid64HexKey_ShouldPassValidation()
    {
        var options = new WebhookEncryptionOptions
        {
            MasterKey = "7f8e9d0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6"
        };

        var validationResults = ValidateModel(options);

        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void EventFlowOptions_WithEmptyApiKey_ShouldFailValidation()
    {
        var options = new EventFlowOptions
        {
            BaseUrl = "https://eventflow.internal",
            ApiKey = string.Empty
        };

        var validationResults = ValidateModel(options);

        validationResults.Should().Contain(v => v.MemberNames.Contains(nameof(EventFlowOptions.ApiKey)));
    }

    [Fact]
    public void EventFlowOptions_WithInvalidBaseUrl_ShouldFailValidation()
    {
        var options = new EventFlowOptions
        {
            BaseUrl = "not_a_valid_url",
            ApiKey = "valid_api_key_12345"
        };

        var validationResults = ValidateModel(options);

        validationResults.Should().Contain(v => v.MemberNames.Contains(nameof(EventFlowOptions.BaseUrl)));
    }

    [Fact]
    public void AddInfrastructureServices_InProduction_WithoutConnectionString_ShouldThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Jwt:SecretKey"] = "valid_secure_jwt_secret_key_at_least_32_characters_long_2026!",
                ["WebhookEncryption:MasterKey"] = "7f8e9d0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6",
                ["EventFlow:BaseUrl"] = "http://localhost:5000",
                ["EventFlow:ApiKey"] = "valid_eventflow_key_12345"
            })
            .Build();

        var services = new ServiceCollection();

        var action = () => services.AddInfrastructureServices(config);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*'ConnectionStrings:DefaultConnection' must be set in Production*");
    }

    [Fact]
    public void ValidateOnStart_WithMissingJwtSecret_ShouldFailAtStartup()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["Jwt:Issuer"] = "HookBridge",
                ["Jwt:Audience"] = "Clients",
                ["Jwt:SecretKey"] = "",
                ["WebhookEncryption:MasterKey"] = "7f8e9d0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6",
                ["EventFlow:BaseUrl"] = "http://localhost:5000",
                ["EventFlow:ApiKey"] = "valid_eventflow_key_12345"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(config);

        var sp = services.BuildServiceProvider();

        var action = () => sp.GetRequiredService<IOptions<JwtOptions>>().Value;

        action.Should().Throw<OptionsValidationException>();
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var context = new ValidationContext(model, null, null);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
