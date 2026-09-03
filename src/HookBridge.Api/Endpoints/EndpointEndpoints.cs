using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Endpoints;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class EndpointEndpoints
{
    public static IEndpointRouteBuilder MapEndpointEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/endpoints")
            .WithTags("Endpoints")
            .RequireAuthorization();

        // 1. Create Endpoint (Requires Developer)
        group.MapPost("/", async (
            [FromBody] CreateEndpointCommand command,
            [FromServices] CreateEndpointUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status201Created);
        })
        .WithName("CreateEndpoint")
        .WithSummary("Registers a new webhook destination endpoint, generates initial HMAC signing secret, and binds subscriptions.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<EndpointCreatedResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 2. List Endpoints (Requires Viewer)
        group.MapGet("/", async (
            [FromQuery] Guid? appId,
            [FromServices] GetEndpointsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(appId, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetEndpoints")
        .WithSummary("Lists all endpoints within the active tenant, optionally filtered by applicationId.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<IReadOnlyList<EndpointResponse>>(StatusCodes.Status200OK);

        // 3. Get Endpoint by ID (Requires Viewer)
        group.MapGet("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] GetEndpointByIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetEndpointById")
        .WithSummary("Retrieves destination endpoint details, active signing secret prefix, and subscribed event patterns.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<EndpointResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 4. Update Endpoint (Requires Developer)
        group.MapPut("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateEndpointCommand command,
            [FromServices] UpdateEndpointUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("UpdateEndpoint")
        .WithSummary("Updates target URL, rate limit, timeout, and description with SSRF pre-validation.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<EndpointResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 5. Update Endpoint Status (Requires Developer)
        group.MapPatch("/{id:guid}/status", async (
            [FromRoute] Guid id,
            [FromBody] UpdateEndpointStatusCommand command,
            [FromServices] UpdateEndpointStatusUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("UpdateEndpointStatus")
        .WithSummary("Updates endpoint operational status (Active, Paused, Disabled).")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<EndpointResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 6. Delete Endpoint (Requires TenantAdmin)
        group.MapDelete("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] DeleteEndpointUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status204NoContent);
        })
        .WithName("DeleteEndpoint")
        .WithSummary("Deletes an endpoint, its webhook secrets, and subscriptions.")
        .RequireAuthorization(AuthorizationPolicies.RequireTenantAdmin)
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        return app;
    }
}
