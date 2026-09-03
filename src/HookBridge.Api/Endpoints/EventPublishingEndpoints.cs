using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.UseCases.Publishing;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class EventPublishingEndpoints
{
    public static IEndpointRouteBuilder MapEventPublishingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/events")
            .WithTags("Events")
            .RequireAuthorization();

        // 1. Publish Event (Requires Developer)
        group.MapPost("/", async (
            [FromBody] PublishEventCommand command,
            [FromServices] PublishEventUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status202Accepted);
        })
        .WithName("PublishEvent")
        .WithSummary("Publishes an event into the HookBridge ingestion pipeline, provisions delivery schedules for matched endpoint subscriptions, and forwards to EventFlow Data Plane.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<PublishEventResponse>(StatusCodes.Status202Accepted)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return app;
    }
}
