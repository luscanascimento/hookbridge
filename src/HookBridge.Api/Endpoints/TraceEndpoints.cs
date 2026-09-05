using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Traces;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class TraceEndpoints
{
    public static IEndpointRouteBuilder MapTraceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/traces")
            .WithTags("Traces")
            .RequireAuthorization();

        // 1. Query Traces (Filtered & Paginated)
        group.MapGet("/", async (
            [FromQuery] string? query,
            [FromQuery] string? status,
            [FromQuery] DateTimeOffset? fromDate,
            [FromQuery] DateTimeOffset? toDate,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromServices] GetTracesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var q = new TraceQuery(
                query,
                status,
                fromDate,
                toDate,
                page ?? 1,
                pageSize ?? 20);

            var result = await useCase.ExecuteAsync(q, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetTraces")
        .WithSummary("Queries distributed webhook traces correlated across gateway ingestion, outbox, broker, worker, outbound HTTP dispatches, and audit logs.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<PagedList<TraceSummaryResponse>>(StatusCodes.Status200OK);

        // 2. Get Trace Detail (Waterfall DAG & Correlation)
        group.MapGet("/{identifier}", async (
            [FromRoute] string identifier,
            [FromServices] GetTraceDetailUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(identifier, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetTraceDetail")
        .WithSummary("Retrieves the full distributed trace waterfall DAG, span timings, correlated delivery attempts, and audit logs for a given trace ID, correlation ID, or delivery ID.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<TraceDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        return app;
    }
}
