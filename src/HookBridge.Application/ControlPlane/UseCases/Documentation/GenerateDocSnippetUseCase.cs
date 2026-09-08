using HookBridge.Application.ControlPlane.Services;

namespace HookBridge.Application.ControlPlane.UseCases.Documentation;

public sealed class GenerateDocSnippetUseCase
{
    private readonly IDocSnippetGenerator _snippetGenerator;

    public GenerateDocSnippetUseCase(IDocSnippetGenerator snippetGenerator)
    {
        _snippetGenerator = snippetGenerator;
    }

    public GenerateDocSnippetResponse Execute(GenerateDocSnippetRequest request, string? baseUrl = null)
    {
        var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.hookbridge.io" : baseUrl.TrimEnd('/');
        var targetUrl = request.Path.StartsWith("http", StringComparison.OrdinalIgnoreCase) 
            ? request.Path 
            : $"{effectiveBaseUrl}/{request.Path.TrimStart('/')}";

        var headers = request.Headers ?? new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(request.ApiKey) && !headers.ContainsKey("Authorization"))
        {
            headers["Authorization"] = $"Bearer {request.ApiKey}";
        }
        if (!string.IsNullOrWhiteSpace(request.Body) && !headers.ContainsKey("Content-Type"))
        {
            headers["Content-Type"] = "application/json";
        }

        var snippet = _snippetGenerator.GenerateGenericSnippet(
            request.Language,
            request.Method,
            targetUrl,
            headers,
            request.Body
        );

        var curl = _snippetGenerator.GenerateGenericSnippet(
            "curl",
            request.Method,
            targetUrl,
            headers,
            request.Body
        );

        return new GenerateDocSnippetResponse(
            Language: request.Language,
            Snippet: snippet,
            FormattedCurl: curl
        );
    }
}
