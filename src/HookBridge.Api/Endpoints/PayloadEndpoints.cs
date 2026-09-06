using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Payloads;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class PayloadEndpoints
{
    public static IEndpointRouteBuilder MapPayloadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/payloads")
            .WithTags("Payloads")
            .RequireAuthorization();

        // 1. Analyze Payload Structure & Metrics
        group.MapPost("/analyze", (
            [FromBody] AnalyzePayloadRequest request,
            [FromServices] AnalyzePayloadUseCase useCase) =>
        {
            var result = useCase.Execute(request);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("AnalyzePayload")
        .WithSummary("Analyzes payload structure, byte size, Gzip compression ratio, UTF-8 multibyte characters, and infers JSON schema.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<PayloadAnalysisResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // 2. Evaluate JSONPath Query
        group.MapPost("/jsonpath", (
            [FromBody] EvaluateJsonPathRequest request,
            [FromServices] EvaluateJsonPathUseCase useCase) =>
        {
            var result = useCase.Execute(request);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("EvaluateJsonPath")
        .WithSummary("Evaluates JSONPath expressions against a payload and returns matching paths, values, and types.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<JsonPathEvaluationResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // 3. Diff Payloads
        group.MapPost("/diff", (
            [FromBody] DiffPayloadsRequest request,
            [FromServices] DiffPayloadsUseCase useCase) =>
        {
            var result = useCase.Execute(request);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("DiffPayloads")
        .WithSummary("Compares two JSON payloads and outputs added, removed, modified, and unchanged paths with values and byte deltas.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<PayloadDiffResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // 4. Validate Payload against JSON Schema
        group.MapPost("/validate", (
            [FromBody] ValidatePayloadSchemaRequest request,
            [FromServices] ValidatePayloadSchemaUseCase useCase) =>
        {
            var result = useCase.Execute(request);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("ValidatePayloadSchema")
        .WithSummary("Validates a JSON payload against a specified JSON Schema.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<PayloadSchemaValidationResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return app;
    }
}
