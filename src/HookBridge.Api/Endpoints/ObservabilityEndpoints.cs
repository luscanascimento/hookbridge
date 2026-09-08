using HookBridge.Api.Common;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.UseCases.Observability;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class ObservabilityEndpoints
{
    public static IEndpointRouteBuilder MapObservabilityEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Prometheus Scrape Endpoint (Public/Scraper format)
        app.MapGet("/metrics", async (
            [FromServices] GetPrometheusMetricsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var text = await useCase.ExecuteAsync(cancellationToken);
            return Results.Content(text, "text/plain; version=0.0.4; charset=utf-8");
        })
        .WithName("PrometheusMetrics")
        .WithSummary("Prometheus scrape endpoint exposing OpenTelemetry and runtime metrics.")
        .WithTags("Observability")
        .AllowAnonymous();

        // 2. Observability Management Endpoints Group
        var group = app.MapGroup("/api/v1/observability")
            .WithTags("Observability")
            .RequireAuthorization();

        // Summary
        group.MapGet("/summary", async (
            [FromServices] GetObservabilitySummaryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetObservabilitySummary")
        .WithSummary("Retrieves runtime telemetry summary, active instruments, and memory/GC statistics.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<ObservabilitySummaryResponse>(StatusCodes.Status200OK);

        // Instruments
        group.MapGet("/instruments", async (
            [FromServices] GetMetricInstrumentsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetMetricInstruments")
        .WithSummary("Lists all registered OpenTelemetry metric instruments and their current counter values.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<IReadOnlyList<MetricInstrumentDto>>(StatusCodes.Status200OK);

        // Spans
        group.MapGet("/spans", async (
            [FromQuery] int? count,
            [FromServices] GetRecentCapturedSpansUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(count ?? 50, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetRecentSpans")
        .WithSummary("Retrieves recent distributed spans captured in the OpenTelemetry telemetry buffer.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<IReadOnlyList<CapturedSpanDto>>(StatusCodes.Status200OK);

        // Synthetic Trace Pipeline Test
        group.MapPost("/synthetic-trace", async (
            [FromBody] SyntheticTraceCommand command,
            [FromServices] GenerateSyntheticTraceUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GenerateSyntheticTrace")
        .WithSummary("Generates a synthetic distributed trace across gateway, crypto signing, DB outbox, and HTTP dispatch.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<SyntheticTraceResponse>(StatusCodes.Status200OK);

        return app;
    }
}
