using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.WebhookSigning;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class WebhookSignatureEndpoints
{
    public static IEndpointRouteBuilder MapWebhookSignatureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/endpoints/{endpointId:guid}/signatures")
            .WithTags("Webhook Signatures")
            .RequireAuthorization();

        // 1. Generate Signature for Payload (Developer Portal & Testing)
        group.MapPost("/generate", async (
            [FromRoute] Guid endpointId,
            [FromBody] GenerateSignatureCommand command,
            [FromServices] GenerateEndpointSignatureUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(endpointId, command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GenerateWebhookSignature")
        .WithSummary("Generates an authentic HMAC-SHA256 X-HookBridge-Signature header for testing webhook verification algorithms.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<GenerateSignatureResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // 2. Verify Signature for Payload (Developer Testing / Debugger)
        group.MapPost("/verify", async (
            [FromRoute] Guid endpointId,
            [FromBody] VerifySignatureCommand command,
            [FromServices] VerifyEndpointSignatureUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(endpointId, command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("VerifyWebhookSignature")
        .WithSummary("Tests webhook signature verification against active and rotating endpoint keys with anti-replay tolerance checking.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<VerifySignatureResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return app;
    }
}
