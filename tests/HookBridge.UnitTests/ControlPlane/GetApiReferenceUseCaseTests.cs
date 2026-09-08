using FluentAssertions;
using HookBridge.Application.ControlPlane.Services;
using HookBridge.Application.ControlPlane.UseCases.Documentation;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class GetApiReferenceUseCaseTests
{
    private readonly DocSnippetGenerator _snippetGenerator = new();

    [Fact]
    public void Execute_ShouldReturnComprehensiveReferenceWithGuidesEndpointsAndSdkRecipes()
    {
        // Arrange
        var useCase = new GetApiReferenceUseCase(_snippetGenerator);

        // Act
        var response = useCase.Execute("https://custom-gateway.io", "hb_live_custom_key");

        // Assert
        response.Should().NotBeNull();
        response.BaseUrl.Should().Be("https://custom-gateway.io");
        response.Version.Should().Be("1.0.0");

        // Guides check
        response.Guides.Should().NotBeEmpty();
        response.Guides.Should().Contain(g => g.Id == "quickstart");
        response.Guides.Should().Contain(g => g.Id == "webhook-signing");
        response.Guides.Should().Contain(g => g.Id == "retries-and-idempotency");
        response.Guides.Should().Contain(g => g.Id == "secret-rotation");

        // Endpoints check
        response.Endpoints.Should().NotBeEmpty();
        response.Endpoints.Should().Contain(e => e.Path == "/api/v1/events/publish");
        response.Endpoints.Should().Contain(e => e.Path == "/api/v1/endpoints");
        response.Endpoints.Should().Contain(e => e.Path == "/api/v1/deliveries");
        response.Endpoints.Should().Contain(e => e.Path == "/api/v1/traces/{identifier}");
        response.Endpoints.Should().Contain(e => e.Path == "/api/v1/webhook-signatures/verify");

        // Sdk Recipes check
        response.SdkRecipes.Should().NotBeEmpty();
        response.SdkRecipes.Should().Contain(r => r.Language == "typescript");
        response.SdkRecipes.Should().Contain(r => r.Language == "csharp");
        response.SdkRecipes.Should().Contain(r => r.Language == "python");
        response.SdkRecipes.Should().Contain(r => r.Language == "go");
        response.SdkRecipes.Should().Contain(r => r.Language == "php");
    }

    [Fact]
    public void GenerateDocSnippetUseCase_ShouldGenerateFormattedSnippets()
    {
        // Arrange
        var useCase = new GenerateDocSnippetUseCase(_snippetGenerator);
        var request = new GenerateDocSnippetRequest(
            Language: "python",
            Method: "POST",
            Path: "/api/v1/events/publish",
            Headers: new Dictionary<string, string> { ["X-Tenant-Id"] = "tenant-123" },
            Body: "{\"eventType\":\"order.created\"}",
            ApiKey: "hb_live_abc",
            SigningSecret: null
        );

        // Act
        var response = useCase.Execute(request, "https://api.hookbridge.io");

        // Assert
        response.Should().NotBeNull();
        response.Language.Should().Be("python");
        response.Snippet.Should().Contain("requests.post");
        response.Snippet.Should().Contain("hb_live_abc");
        response.FormattedCurl.Should().Contain("curl -X POST");
        response.FormattedCurl.Should().Contain("hb_live_abc");
    }
}
