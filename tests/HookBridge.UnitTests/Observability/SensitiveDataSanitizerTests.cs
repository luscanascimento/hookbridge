using HookBridge.Domain.Security;
using Xunit;

namespace HookBridge.UnitTests.Observability;

public sealed class SensitiveDataSanitizerTests
{
    [Theory]
    [InlineData("Authorization", true)]
    [InlineData("authorization", true)]
    [InlineData("X-Api-Key", true)]
    [InlineData("Cookie", true)]
    [InlineData("Set-Cookie", true)]
    [InlineData("X-HookBridge-Secret", true)]
    [InlineData("X-Auth-Token", true)]
    [InlineData("Private-Key", true)]
    [InlineData("Content-Type", false)]
    [InlineData("User-Agent", false)]
    [InlineData("Accept", false)]
    [InlineData("Host", false)]
    public void IsSensitiveHeader_ClassifiesCorrectly(string headerName, bool expected)
    {
        var result = SensitiveDataSanitizer.IsSensitiveHeader(headerName);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("password", true)]
    [InlineData("adminPassword", true)]
    [InlineData("secretKey", true)]
    [InlineData("clientSecret", true)]
    [InlineData("apiKey", true)]
    [InlineData("plainKey", true)]
    [InlineData("token", true)]
    [InlineData("accessToken", true)]
    [InlineData("refreshToken", true)]
    [InlineData("cvv", true)]
    [InlineData("creditCard", true)]
    [InlineData("eventType", false)]
    [InlineData("id", false)]
    [InlineData("tenantId", false)]
    [InlineData("name", false)]
    [InlineData("status", false)]
    public void IsSensitiveProperty_ClassifiesCorrectly(string propertyName, bool expected)
    {
        var result = SensitiveDataSanitizer.IsSensitiveProperty(propertyName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeHeader_RedactsBearerToken()
    {
        var sanitized = SensitiveDataSanitizer.SanitizeHeader("Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.xyz");
        Assert.Equal("Bearer [REDACTED]", sanitized);
    }

    [Fact]
    public void SanitizeHeader_RedactsBasicAuth()
    {
        var sanitized = SensitiveDataSanitizer.SanitizeHeader("Authorization", "Basic dXNlcjpwYXNzd29yZA==");
        Assert.Equal("Basic [REDACTED]", sanitized);
    }

    [Fact]
    public void SanitizeHeader_MasksApiKey()
    {
        var sanitized = SensitiveDataSanitizer.SanitizeHeader("X-Api-Key", "hb_live_1234567890abcdef9999");
        Assert.Equal("hb_live_...9999", sanitized);
    }

    [Fact]
    public void SanitizeHeader_PreservesInnocentHeader()
    {
        var sanitized = SensitiveDataSanitizer.SanitizeHeader("Content-Type", "application/json");
        Assert.Equal("application/json", sanitized);
    }

    [Fact]
    public void SanitizeHeadersJson_RedactsSensitiveHeaders()
    {
        var headersJson = "{\"Content-Type\":\"application/json\",\"Authorization\":\"Bearer secret_token_123\",\"X-Api-Key\":\"hb_live_secret12345678abcd\"}";
        var result = SensitiveDataSanitizer.SanitizeHeadersJson(headersJson);

        Assert.Contains("\"Content-Type\":\"application/json\"", result);
        Assert.Contains("\"Authorization\":\"Bearer [REDACTED]\"", result);
        Assert.Contains("\"X-Api-Key\":\"hb_live_...abcd\"", result);
    }

    [Fact]
    public void SanitizeJson_RedactsDeeplyNestedSensitiveProperties()
    {
        var payload = """
        {
            "event": "user.created",
            "user": {
                "id": "123",
                "email": "user@example.com",
                "password": "super_secret_plain_password",
                "tokens": [
                    { "type": "access", "token": "jwt_token_value_abc" }
                ]
            },
            "metadata": {
                "clientSecret": "sec_98765",
                "environment": "production"
            }
        }
        """;

        var sanitized = SensitiveDataSanitizer.SanitizeJson(payload);

        Assert.Contains("\"user.created\"", sanitized);
        Assert.Contains("\"user@example.com\"", sanitized);
        Assert.Contains("\"environment\":\"production\"", sanitized);
        Assert.DoesNotContain("super_secret_plain_password", sanitized);
        Assert.DoesNotContain("jwt_token_value_abc", sanitized);
        Assert.DoesNotContain("sec_98765", sanitized);
        Assert.Contains("\"password\":\"[REDACTED]\"", sanitized);
        Assert.Contains("\"token\":\"[REDACTED]\"", sanitized);
        Assert.Contains("\"clientSecret\":\"[REDACTED]\"", sanitized);
    }

    [Fact]
    public void SanitizeUrl_StripsBasicAuthCredentials()
    {
        var url = "https://svc_admin:SuperSecretPass@api.partner.com/webhook/v1";
        var sanitized = SensitiveDataSanitizer.SanitizeUrl(url);

        Assert.Equal("https://svc_admin:[REDACTED]@api.partner.com/webhook/v1", sanitized);
    }

    [Fact]
    public void SanitizeUrl_RedactsSensitiveQueryParams()
    {
        var url = "https://api.partner.com/webhook?token=secret123&page=1&apiKey=key999";
        var sanitized = SensitiveDataSanitizer.SanitizeUrl(url);

        Assert.Contains("token=[REDACTED]", sanitized);
        Assert.Contains("apiKey=[REDACTED]", sanitized);
        Assert.Contains("page=1", sanitized);
    }

    [Theory]
    [InlineData("admin@hookbridge.io", "a***n@hookbridge.io")]
    [InlineData("john.doe@company.org", "j***e@company.org")]
    [InlineData("me@domain.com", "m*e@domain.com")]
    public void MaskEmail_MasksCorrectly(string email, string expected)
    {
        var masked = SensitiveDataSanitizer.MaskEmail(email);
        Assert.Equal(expected, masked);
    }
}
