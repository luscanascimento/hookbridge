using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Schemas;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class EventSchemaEndpoints
{
    public static IEndpointRouteBuilder MapEventSchemaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/schemas")
            .WithTags("Schemas")
            .RequireAuthorization();

        // 1. List Event Schemas
        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] SchemaStatus? status,
            [FromQuery] SchemaCompatibilityMode? compatibilityMode,
            [FromServices] GetEventSchemasUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(search, status, compatibilityMode, ct);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetEventSchemas")
        .WithSummary("Lists registered event schemas with active versions and compatibility policies.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<IReadOnlyList<EventSchemaSummaryResponse>>(StatusCodes.Status200OK);

        // 2. Create Event Schema
        group.MapPost("/", async (
            [FromBody] CreateEventSchemaRequest request,
            [FromServices] CreateEventSchemaUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return HttpResults.Match(result, StatusCodes.Status201Created);
        })
        .WithName("CreateEventSchema")
        .WithSummary("Registers a new event schema with an initial version and compatibility mode.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<EventSchemaDetailResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // 3. Check Schema Compatibility (Standalone)
        group.MapPost("/compatibility/check", (
            [FromBody] CheckCompatibilityRequest request,
            [FromServices] CheckSchemaCompatibilityUseCase useCase) =>
        {
            var result = useCase.Execute(request);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("CheckSchemaCompatibility")
        .WithSummary("Compares two JSON Schemas and determines compatibility according to the specified mode.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<CheckCompatibilityResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // 4. Validate Payload Against Event Type / Schema
        group.MapPost("/validate", async (
            [FromBody] ValidateEventPayloadRequest request,
            [FromServices] ValidateEventPayloadUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("ValidateEventPayload")
        .WithSummary("Validates a payload against a registered event schema version.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<ValidateEventPayloadResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // 5. Get Schema Details by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] GetEventSchemaByIdUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, ct);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetEventSchemaById")
        .WithSummary("Retrieves detailed event schema information including full version evolution history.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<EventSchemaDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 6. Update Schema Metadata
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateEventSchemaRequest request,
            [FromServices] UpdateEventSchemaUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, request, ct);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("UpdateEventSchema")
        .WithSummary("Updates event schema metadata, description, compatibility policy, or status.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<EventSchemaDetailResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 7. Delete Schema
        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] DeleteEventSchemaUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, ct);
            return HttpResults.Match(result, StatusCodes.Status204NoContent);
        })
        .WithName("DeleteEventSchema")
        .WithSummary("Deletes an event schema and all its associated versions.")
        .RequireAuthorization(AuthorizationPolicies.RequireTenantAdmin)
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 8. Create Schema Version
        group.MapPost("/{id:guid}/versions", async (
            Guid id,
            [FromBody] CreateSchemaVersionRequest request,
            [FromServices] CreateSchemaVersionUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, request, ct);
            return HttpResults.Match(result, StatusCodes.Status201Created);
        })
        .WithName("CreateSchemaVersion")
        .WithSummary("Creates a new version for an event schema, enforcing compatibility constraints.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<EventSchemaVersionResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // 9. Activate Schema Version
        group.MapPost("/{id:guid}/versions/{versionId:guid}/activate", async (
            Guid id,
            Guid versionId,
            [FromServices] ActivateSchemaVersionUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, versionId, ct);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("ActivateSchemaVersion")
        .WithSummary("Sets a specific schema version as the active version for the event type.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<EventSchemaVersionResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 10. Deprecate Schema Version
        group.MapPost("/{id:guid}/versions/{versionId:guid}/deprecate", async (
            Guid id,
            Guid versionId,
            [FromServices] DeprecateSchemaVersionUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, versionId, ct);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("DeprecateSchemaVersion")
        .WithSummary("Marks a schema version as deprecated.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<EventSchemaVersionResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 11. Detect Contract Drift
        group.MapPost("/{id:guid}/drift", async (
            Guid id,
            [FromBody] DetectSchemaDriftRequest? request,
            [FromServices] DetectSchemaDriftUseCase useCase,
            CancellationToken ct) =>
        {
            var req = request ?? new DetectSchemaDriftRequest(id, 50);
            if (req.SchemaId == Guid.Empty) req = req with { SchemaId = id };
            var result = await useCase.ExecuteAsync(req, ct);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("DetectSchemaDrift")
        .WithSummary("Audits recent deliveries of the event type against active schema, reporting conformance rate and contract drift.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<DetectSchemaDriftResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 12. Generate Schema Documentation & Code
        group.MapGet("/{id:guid}/docs", async (
            Guid id,
            [FromQuery] Guid? versionId,
            [FromServices] GenerateSchemaDocsUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(id, versionId, ct);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GenerateSchemaDocs")
        .WithSummary("Generates Markdown documentation, TypeScript interfaces, C# records, and sample payloads for a schema version.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<SchemaDocumentationResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        return app;
    }
}
