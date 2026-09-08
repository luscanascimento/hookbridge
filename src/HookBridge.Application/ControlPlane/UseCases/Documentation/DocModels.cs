namespace HookBridge.Application.ControlPlane.UseCases.Documentation;

public sealed record DocSectionDto(
    string Id,
    string Title,
    string Category,
    string Summary,
    string ContentMarkdown,
    Dictionary<string, string> CodeSnippets
);

public sealed record DocEndpointDto(
    string Id,
    string Category,
    string Method,
    string Path,
    string Summary,
    string Description,
    List<DocParamDto> PathParameters,
    List<DocParamDto> QueryParameters,
    List<DocHeaderDto> RequestHeaders,
    string? SampleRequestBody,
    int ExpectedStatusCode,
    string? SampleResponseBody,
    Dictionary<string, string> CodeSnippets
);

public sealed record DocParamDto(
    string Name,
    string Type,
    bool Required,
    string Description,
    string? Example
);

public sealed record DocHeaderDto(
    string Name,
    bool Required,
    string Description,
    string Example
);

public sealed record SdkRecipeDto(
    string Language,
    string DisplayName,
    string Description,
    string InstallationCommand,
    string VerificationSnippet,
    string PublishingSnippet,
    List<string> Dependencies
);

public sealed record ApiReferenceResponse(
    string Version,
    string Environment,
    string BaseUrl,
    List<DocSectionDto> Guides,
    List<DocEndpointDto> Endpoints,
    List<SdkRecipeDto> SdkRecipes
);

public sealed record GenerateDocSnippetRequest(
    string Language,
    string Method,
    string Path,
    Dictionary<string, string>? Headers,
    string? Body,
    string? ApiKey,
    string? SigningSecret
);

public sealed record GenerateDocSnippetResponse(
    string Language,
    string Snippet,
    string FormattedCurl
);
