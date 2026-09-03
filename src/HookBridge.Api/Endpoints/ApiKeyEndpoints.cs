using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.ApiKeys;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapApiKeyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/api-keys")
            .WithTags("API Keys")
            .RequireAuthorization();

        // 1. Create API Key (Requires TenantAdmin)
        group.MapPost("/", async (
            [FromBody] CreateApiKeyCommand command,
            [FromServices] CreateApiKeyUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status201Created);
        })
        .WithName("CreateApiKey")
        .WithSummary("Issues a new cryptographically generated API Key. The full plaintext key is only returned once upon creation.")
        .RequireAuthorization(AuthorizationPolicies.RequireTenantAdmin)
        .Produces<ApiKeyCreatedResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // 2. List API Keys (Requires TenantAdmin)
        group.MapGet("/", async (
            [FromServices] GetApiKeysUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetApiKeys")
        .WithSummary("Lists active and revoked API keys with key prefixes and permission scopes.")
        .RequireAuthorization(AuthorizationPolicies.RequireTenantAdmin)
        .Produces<IReadOnlyList<ApiKeyResponse>>(StatusCodes.Status200OK);

        // 3. Revoke API Key (Requires TenantAdmin)
        group.MapDelete("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] RevokeApiKeyUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status204NoContent);
        })
        .WithName("RevokeApiKey")
        .WithSummary("Immediately revokes an API Key, invalidating all subsequent requests authenticated with this key.")
        .RequireAuthorization(AuthorizationPolicies.RequireTenantAdmin)
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return app;
    }
}
