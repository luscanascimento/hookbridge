using FluentAssertions;
using HookBridge.Application.ControlPlane.Services;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class DocSnippetGeneratorTests
{
    private readonly DocSnippetGenerator _sut = new();

    [Theory]
    [InlineData("typescript", "createHmac", "timingSafeEqual")]
    [InlineData("csharp", "HMACSHA256", "CryptographicOperations.FixedTimeEquals")]
    [InlineData("python", "hmac.new", "hmac.compare_digest")]
    [InlineData("go", "hmac.New", "subtle.ConstantTimeCompare")]
    [InlineData("php", "hash_hmac", "hash_equals")]
    public void GenerateSignatureVerificationSnippet_ShouldContainCoreCryptographicPrimitives(string language, string expectedHmac, string expectedComparison)
    {
        // Act
        var snippet = _sut.GenerateSignatureVerificationSnippet(language, "whsec_test_secret_123");

        // Assert
        snippet.Should().NotBeNullOrWhiteSpace();
        snippet.Should().Contain(expectedHmac);
        snippet.Should().Contain(expectedComparison);
        snippet.Should().Contain("whsec_test_secret_123");
        snippet.Should().Contain("X-HookBridge-Signature");
    }

    [Theory]
    [InlineData("curl", "curl -X POST", "order.created")]
    [InlineData("typescript", "axios.post", "order.created")]
    [InlineData("csharp", "PostAsJsonAsync", "order.created")]
    [InlineData("python", "requests.post", "order.created")]
    [InlineData("go", "http.NewRequest", "order.created")]
    public void GenerateEventPublishingSnippet_ShouldGenerateValidPublishSnippets(string language, string expectedClient, string expectedEventType)
    {
        // Act
        var snippet = _sut.GenerateEventPublishingSnippet(language, "https://api.hookbridge.io", "hb_live_123", "order.created", "{\"total\": 99.9}");

        // Assert
        snippet.Should().NotBeNullOrWhiteSpace();
        snippet.Should().Contain(expectedClient);
        snippet.Should().Contain(expectedEventType);
        snippet.Should().Contain("hb_live_123");
    }

    [Theory]
    [InlineData("curl", "curl -X POST", "https://api.domain.com/webhooks")]
    [InlineData("typescript", "axios.post", "https://api.domain.com/webhooks")]
    [InlineData("csharp", "PostAsJsonAsync", "https://api.domain.com/webhooks")]
    [InlineData("python", "requests.post", "https://api.domain.com/webhooks")]
    [InlineData("go", "http.NewRequest", "https://api.domain.com/webhooks")]
    public void GenerateEndpointRegistrationSnippet_ShouldGenerateValidSnippets(string language, string expectedClient, string expectedUrl)
    {
        // Act
        var snippet = _sut.GenerateEndpointRegistrationSnippet(
            language,
            "https://api.hookbridge.io",
            "hb_live_key_999",
            expectedUrl,
            "Orders Receiver",
            ["order.*", "invoice.paid"]);

        // Assert
        snippet.Should().NotBeNullOrWhiteSpace();
        snippet.Should().Contain(expectedClient);
        snippet.Should().Contain(expectedUrl);
        snippet.Should().Contain("order.*");
    }

    [Fact]
    public void GenerateGenericSnippet_ShouldGenerateCorrectCurlAndCode()
    {
        // Act
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer hb_live_secret",
            ["X-Custom-Header"] = "test-value"
        };
        var curl = _sut.GenerateGenericSnippet("curl", "POST", "https://api.hookbridge.io/api/v1/schemas", headers, "{\"name\":\"order\"}");
        var ts = _sut.GenerateGenericSnippet("typescript", "POST", "https://api.hookbridge.io/api/v1/schemas", headers, "{\"name\":\"order\"}");

        // Assert
        curl.Should().Contain("curl -X POST \"https://api.hookbridge.io/api/v1/schemas\"");
        curl.Should().Contain("-H \"Authorization: Bearer hb_live_secret\"");
        curl.Should().Contain("-H \"X-Custom-Header: test-value\"");
        curl.Should().Contain("-d '{\"name\":\"order\"}'");

        ts.Should().Contain("axios({");
        ts.Should().Contain("method: 'post'");
        ts.Should().Contain("'Authorization': 'Bearer hb_live_secret'");
    }
}
