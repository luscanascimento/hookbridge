using System.Text;
using System.Text.Json;
using HookBridge.Api.Common;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.UseCases.Sandbox;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class SandboxEndpoints
{
    private static readonly string[] ReceiverHttpMethods = ["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"];

    public static IEndpointRouteBuilder MapSandboxEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Management Endpoints Group (Requires Auth)
        var group = app.MapGroup("/api/v1/sandboxes")
            .WithTags("Webhook Sandbox")
            .RequireAuthorization();

        // Create Sandbox
        group.MapPost("/", async (
            [FromBody] CreateSandboxCommand command,
            [FromServices] CreateSandboxUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var result = await useCase.ExecuteAsync(command, hostUrl, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status201Created);
        })
        .WithName("CreateSandbox")
        .WithSummary("Creates a new webhook sandbox receiver with custom response simulation rules.")
        .Produces<WebhookSandboxResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // List Sandboxes
        group.MapGet("/", async (
            [FromServices] GetSandboxesUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var result = await useCase.ExecuteAsync(hostUrl, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetSandboxes")
        .WithSummary("Lists all active webhook sandboxes for the authenticated tenant.")
        .Produces<IReadOnlyList<WebhookSandboxResponse>>(StatusCodes.Status200OK);

        // Get Sandbox by ID
        group.MapGet("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] GetSandboxByIdUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var result = await useCase.ExecuteAsync(id, hostUrl, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetSandboxById")
        .WithSummary("Gets configuration details for a specific webhook sandbox.")
        .Produces<WebhookSandboxResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Update Sandbox Config
        group.MapPut("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateSandboxConfigCommand command,
            [FromServices] UpdateSandboxConfigUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var result = await useCase.ExecuteAsync(id, command, hostUrl, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("UpdateSandboxConfig")
        .WithSummary("Updates response code, latency delay, and simulation body for a webhook sandbox.")
        .Produces<WebhookSandboxResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Delete Sandbox
        group.MapDelete("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] DeleteSandboxUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status204NoContent);
        })
        .WithName("DeleteSandbox")
        .WithSummary("Deletes a webhook sandbox and all its captured request history.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Get Captured Requests
        group.MapGet("/{id:guid}/requests", async (
            [FromRoute] Guid id,
            [FromQuery] string? method,
            [FromQuery] string? search,
            [FromQuery] int? statusCode,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromServices] GetSandboxRequestsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(
                id,
                method,
                search,
                statusCode,
                page ?? 1,
                pageSize ?? 50,
                cancellationToken
            );
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetSandboxRequests")
        .WithSummary("Retrieves captured webhook requests for a sandbox with filtering and pagination.")
        .Produces<PagedSandboxRequestsResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Get Captured Request Details
        group.MapGet("/{id:guid}/requests/{requestId:guid}", async (
            [FromRoute] Guid id,
            [FromRoute] Guid requestId,
            [FromServices] GetSandboxRequestByIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, requestId, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetSandboxRequestById")
        .WithSummary("Gets deep inspection details (headers, body, query, IP) for a captured request.")
        .Produces<SandboxRequestResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Clear Captured Requests
        group.MapDelete("/{id:guid}/requests", async (
            [FromRoute] Guid id,
            [FromServices] ClearSandboxRequestsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status204NoContent);
        })
        .WithName("ClearSandboxRequests")
        .WithSummary("Clears all captured requests for a sandbox.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // 2. Public Webhook Receiver Endpoint (Anonymous / Open for incoming webhooks)
        app.MapMethods("/api/v1/sandbox/receiver/{slug}", ReceiverHttpMethods, async (
            [FromRoute] string slug,
            [FromServices] ProcessSandboxRequestUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var req = httpContext.Request;
            var path = req.Path.Value ?? string.Empty;
            var query = req.QueryString.HasValue ? req.QueryString.Value : null;

            // Extract headers
            var headersDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in req.Headers)
            {
                headersDict[k] = v.ToString();
            }
            var headersJson = JsonSerializer.Serialize(headersDict);

            // Read raw body
            string? body = null;
            if (req.ContentLength > 0 || req.Body != null)
            {
                using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
                body = await reader.ReadToEndAsync(cancellationToken);
            }

            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
            var contentType = req.ContentType;
            var contentLength = req.ContentLength ?? (body != null ? Encoding.UTF8.GetByteCount(body) : 0);

            var result = await useCase.ExecuteAsync(
                slug,
                req.Method,
                path,
                query,
                headersJson,
                body,
                contentType,
                contentLength,
                clientIp,
                cancellationToken
            );

            if (result.IsFailure)
            {
                return Results.Problem(
                    statusCode: result.Error.Code.Contains("NotFound", StringComparison.OrdinalIgnoreCase) ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest,
                    title: result.Error.Code,
                    detail: result.Error.Message
                );
            }

            var sim = result.Value;
            return Results.Content(
                content: sim.Body ?? string.Empty,
                contentType: sim.ContentType,
                statusCode: sim.StatusCode
            );
        })
        .WithName("ReceiveSandboxWebhook")
        .WithSummary("Public receiver endpoint that captures incoming webhooks, applies response simulation, and streams events.")
        .AllowAnonymous();

        return app;
    }
}
