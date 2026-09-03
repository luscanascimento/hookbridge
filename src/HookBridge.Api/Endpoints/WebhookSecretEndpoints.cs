using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.WebhookSecrets;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class WebhookSecretEndpoints
{
    public static IEndpointRouteBuilder MapWebhookSecretEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/endpoints/{endpointId:guid}/secrets")
            .WithTags("Webhook Secrets")
            .RequireAuthorization();

        // 1. List Secrets for Endpoint (Requires Developer)
        group.MapGet("/", async (
            [FromRoute] Guid endpointId,
            [FromServices] GetEndpointSecretsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(endpointId, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetEndpointSecrets")
        .WithSummary("Lists all signing secret metadata (prefix, version, status) for an endpoint without exposing secret values.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<IReadOnlyList<WebhookSecretResponse>>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 2. Rotate Webhook Secret (Requires Developer)
        group.MapPost("/rotate", async (
            [FromRoute] Guid endpointId,
            [FromServices] RotateWebhookSecretUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(endpointId, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status201Created);
        })
        .WithName("RotateWebhookSecret")
        .WithSummary("Rotates signing secret to a new version, setting previous key to Rotating for dual-verification tolerance window.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<RotateSecretResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 3. Revoke Webhook Secret (Requires TenantAdmin)
        group.MapDelete("/{secretId:guid}", async (
            [FromRoute] Guid endpointId,
            [FromRoute] Guid secretId,
            [FromServices] RevokeWebhookSecretUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(endpointId, secretId, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status204NoContent);
        })
        .WithName("RevokeWebhookSecret")
        .WithSummary("Immediately revokes a specific signing secret version.")
        .RequireAuthorization(AuthorizationPolicies.RequireTenantAdmin)
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return app;
    }
}
