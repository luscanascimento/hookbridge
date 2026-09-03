using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Applications;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/apps")
            .WithTags("Applications")
            .RequireAuthorization();

        // 1. Create Application (Requires Developer)
        group.MapPost("/", async (
            [FromBody] CreateApplicationCommand command,
            [FromServices] CreateApplicationUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status201Created);
        })
        .WithName("CreateApplication")
        .WithSummary("Creates a new logical application grouping under the active tenant.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<ApplicationResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // 2. List Applications (Requires Viewer)
        group.MapGet("/", async (
            [FromServices] GetApplicationsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetApplications")
        .WithSummary("Lists all applications registered within the active tenant boundary.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<IReadOnlyList<ApplicationSummaryResponse>>(StatusCodes.Status200OK);

        // 3. Get Application by ID (Requires Viewer)
        group.MapGet("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] GetApplicationByIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetApplicationById")
        .WithSummary("Retrieves detailed application metadata by ID.")
        .RequireAuthorization(AuthorizationPolicies.RequireViewer)
        .Produces<ApplicationResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 4. Update Application (Requires Developer)
        group.MapPut("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateApplicationCommand command,
            [FromServices] UpdateApplicationUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("UpdateApplication")
        .WithSummary("Updates application name, description, and status.")
        .RequireAuthorization(AuthorizationPolicies.RequireDeveloper)
        .Produces<ApplicationResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // 5. Delete Application (Requires TenantAdmin)
        group.MapDelete("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] DeleteApplicationUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status204NoContent);
        })
        .WithName("DeleteApplication")
        .WithSummary("Deletes an application and cascades deletion to all child endpoints and subscriptions.")
        .RequireAuthorization(AuthorizationPolicies.RequireTenantAdmin)
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        return app;
    }
}
