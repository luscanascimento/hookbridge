using HookBridge.Application.ControlPlane.UseCases.Documentation;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class DocEndpoints
{
    public static IEndpointRouteBuilder MapDocEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/docs")
            .WithTags("Developer Documentation");

        // 1. Get API Reference, Guides, and SDK Recipes
        group.MapGet("/reference", (
            [FromQuery] string? baseUrl,
            [FromQuery] string? apiKey,
            [FromServices] GetApiReferenceUseCase useCase,
            HttpContext httpContext) =>
        {
            var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? hostUrl : baseUrl;
            var response = useCase.Execute(effectiveBaseUrl, apiKey);
            return Results.Ok(response);
        })
        .WithName("GetApiReference")
        .WithSummary("Retrieves structured API documentation, quickstart guides, and SDK integration recipes.")
        .Produces<ApiReferenceResponse>(StatusCodes.Status200OK);

        // 2. Generate Dynamic Code Snippet
        group.MapPost("/snippets", (
            [FromBody] GenerateDocSnippetRequest request,
            [FromServices] GenerateDocSnippetUseCase useCase,
            HttpContext httpContext) =>
        {
            var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var response = useCase.Execute(request, hostUrl);
            return Results.Ok(response);
        })
        .WithName("GenerateDocSnippet")
        .WithSummary("Generates tailored code snippets across cURL, TypeScript, C#, Python, and Go for any API request.")
        .Produces<GenerateDocSnippetResponse>(StatusCodes.Status200OK);

        // 3. Get Single SDK Recipe
        group.MapGet("/sdk-recipes/{language}", (
            [FromRoute] string language,
            [FromQuery] string? baseUrl,
            [FromQuery] string? apiKey,
            [FromServices] GetApiReferenceUseCase useCase,
            HttpContext httpContext) =>
        {
            var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? hostUrl : baseUrl;
            var reference = useCase.Execute(effectiveBaseUrl, apiKey);
            
            var recipe = reference.SdkRecipes.FirstOrDefault(r => 
                string.Equals(r.Language, language, StringComparison.OrdinalIgnoreCase));

            return recipe is not null ? Results.Ok(recipe) : Results.NotFound(new { error = $"SDK recipe for '{language}' was not found." });
        })
        .WithName("GetSdkRecipe")
        .WithSummary("Retrieves complete webhook integration recipe and verification code for a specific programming language.")
        .Produces<SdkRecipeDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
