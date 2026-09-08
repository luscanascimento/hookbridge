using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.ControlPlane.UseCases.Documentation;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class DocEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DocEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetApiReference_ShouldReturnSuccessWithGuidesAndEndpoints()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/docs/reference?baseUrl=https://api.hookbridge.io");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ApiReferenceResponse>(JsonOptions);
        
        content.Should().NotBeNull();
        content!.Version.Should().Be("1.0.0");
        content.BaseUrl.Should().Be("https://api.hookbridge.io");
        content.Guides.Should().NotBeEmpty();
        content.Endpoints.Should().NotBeEmpty();
        content.SdkRecipes.Should().NotBeEmpty();

        content.Guides.Should().Contain(g => g.Id == "webhook-signing");
        content.Endpoints.Should().Contain(e => e.Path == "/api/v1/events/publish");
        content.SdkRecipes.Should().Contain(r => r.Language == "typescript");
    }

    [Fact]
    public async Task GenerateDocSnippet_ShouldReturnDynamicCodeAndCurl()
    {
        // Arrange
        var request = new GenerateDocSnippetRequest(
            Language: "typescript",
            Method: "POST",
            Path: "/api/v1/events/publish",
            Headers: new Dictionary<string, string> { ["X-Correlation-Id"] = "corr-123" },
            Body: "{\"eventType\": \"payment.succeeded\"}",
            ApiKey: "hb_live_custom_key_999",
            SigningSecret: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/docs/snippets", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<GenerateDocSnippetResponse>(JsonOptions);

        content.Should().NotBeNull();
        content!.Language.Should().Be("typescript");
        content.Snippet.Should().Contain("axios");
        content.Snippet.Should().Contain("hb_live_custom_key_999");
        content.FormattedCurl.Should().Contain("curl -X POST");
    }

    [Fact]
    public async Task GetSdkRecipe_ShouldReturnRecipeForValidLanguage()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/docs/sdk-recipes/csharp");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var recipe = await response.Content.ReadFromJsonAsync<SdkRecipeDto>(JsonOptions);

        recipe.Should().NotBeNull();
        recipe!.Language.Should().Be("csharp");
        recipe.DisplayName.Should().Contain("C#");
        recipe.VerificationSnippet.Should().Contain("HMACSHA256");
    }

    [Fact]
    public async Task GetSdkRecipe_ShouldReturn404ForUnknownLanguage()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/docs/sdk-recipes/unknown-lang-xyz");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
