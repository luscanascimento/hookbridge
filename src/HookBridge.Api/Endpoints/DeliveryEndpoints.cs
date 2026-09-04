using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Deliveries;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class DeliveryEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/deliveries")
            .WithTags("Deliveries")
            .RequireAuthorization();

        // 1. Query Deliveries (Filtered & Paginated)
        group.MapGet("/", async (
            [FromQuery] Guid? endpointId,
            [FromQuery] DeliveryStatus? status,
            [FromQuery] string? eventType,
            [FromQuery] DateTimeOffset? fromDate,
            [FromQuery] DateTimeOffset? toDate,
            [FromQuery] string? correlationId,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromServices] GetDeliveriesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var query = new GetDeliveriesQuery(
                endpointId,
                status,
                eventType,
                fromDate,
                toDate,
                correlationId,
                page ?? 1,
                pageSize ?? 20);

            var result = await useCase.ExecuteAsync(query, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetDeliveries")
        .WithSummary("Queries historical and pending deliveries with filtering by endpoint, status, event type, date range, and correlation ID.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<PagedList<DeliveryResponse>>(StatusCodes.Status200OK);

        // 2. Get Delivery Stats (Success rate, latency, totals)
        group.MapGet("/stats", async (
            [FromServices] GetDeliveryStatsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetDeliveryStats")
        .WithSummary("Returns aggregated delivery health statistics including success rate, average latency, and state totals.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<DeliveryStatsResponse>(StatusCodes.Status200OK);

        // 3. Get Delivery Details by ID (with attempts timeline)
        group.MapGet("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] GetDeliveryByIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetDeliveryById")
        .WithSummary("Retrieves a delivery record by ID including its complete execution attempts timeline.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<DeliveryDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 4. Record Delivery Attempt (Internal / Worker / Simulator)
        group.MapPost("/{id:guid}/attempts", async (
            [FromRoute] Guid id,
            [FromBody] RecordDeliveryAttemptCommand command,
            [FromServices] RecordDeliveryAttemptUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status201Created);
        })
        .WithName("RecordDeliveryAttempt")
        .WithSummary("Records an outbound delivery transmission attempt and updates delivery status.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<AttemptResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 5. Replay Single Delivery
        group.MapPost("/{id:guid}/replay", async (
            [FromRoute] Guid id,
            [FromBody] ReplayDeliveryCommand? command,
            [FromServices] ReplayDeliveryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("ReplayDelivery")
        .WithSummary("Safely replays an existing webhook delivery with new cryptographic signature headers while preserving lineage.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<ReplayDeliveryResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 6. Bulk Replay Deliveries
        group.MapPost("/replay", async (
            [FromBody] BulkReplayDeliveriesCommand command,
            [FromServices] BulkReplayDeliveriesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("BulkReplayDeliveries")
        .WithSummary("Replays batches of failed or filtered deliveries asynchronously via EventFlow.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<BulkReplayDeliveriesResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // 7. Get Delivery Lineage
        group.MapGet("/{id:guid}/lineage", async (
            [FromRoute] Guid id,
            [FromServices] GetDeliveryLineageUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetDeliveryLineage")
        .WithSummary("Retrieves the full ancestry chain and child replays for a given delivery.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<DeliveryLineageResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        return app;
    }
}
