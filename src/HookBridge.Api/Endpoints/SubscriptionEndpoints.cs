using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Subscriptions;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/endpoints/{endpointId:guid}/subscriptions")
            .WithTags("Subscriptions")
            .RequireAuthorization();

        // 1. Add Subscription to Endpoint (Requires Developer)
        group.MapPost("/", async (
            [FromRoute] Guid endpointId,
            [FromBody] CreateSubscriptionCommand command,
            [FromServices] CreateSubscriptionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(endpointId, command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status201Created);
        })
        .WithName("CreateSubscription")
        .WithSummary("Binds an event type pattern (e.g. 'order.created', 'payment.*', '*') to an endpoint.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<SubscriptionResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // 2. List Subscriptions for Endpoint (Requires Viewer)
        group.MapGet("/", async (
            [FromRoute] Guid endpointId,
            [FromServices] GetSubscriptionsByEndpointUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(endpointId, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetSubscriptionsByEndpoint")
        .WithSummary("Lists all active event subscriptions configured on an endpoint.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<IReadOnlyList<SubscriptionResponse>>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 3. Delete Subscription (Requires Developer)
        group.MapDelete("/{subscriptionId:guid}", async (
            [FromRoute] Guid endpointId,
            [FromRoute] Guid subscriptionId,
            [FromServices] DeleteSubscriptionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(endpointId, subscriptionId, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status204NoContent);
        })
        .WithName("DeleteSubscription")
        .WithSummary("Removes an event subscription from an endpoint.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        return app;
    }
}
