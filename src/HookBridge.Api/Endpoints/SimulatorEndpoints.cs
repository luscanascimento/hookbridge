using System.Text;
using System.Text.Json;
using HookBridge.Api.Common;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.UseCases.Simulator;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class SimulatorEndpoints
{
    private static readonly string[] ReceiverHttpMethods = ["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"];

    public static IEndpointRouteBuilder MapSimulatorEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Management Endpoints Group (Requires Auth)
        var group = app.MapGroup("/api/v1/simulator")
            .WithTags("Delivery Failure Simulator")
            .RequireAuthorization();

        // Create Rule
        group.MapPost("/rules", async (
            [FromBody] CreateSimulatorRuleCommand command,
            [FromServices] CreateSimulatorRuleUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var result = await useCase.ExecuteAsync(command, hostUrl, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status201Created);
        })
        .WithName("CreateSimulatorRule")
        .WithSummary("Creates a new failure simulator rule for chaos and error testing.")
        .Produces<SimulatorRuleResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // List Rules
        group.MapGet("/rules", async (
            [FromServices] GetSimulatorRulesUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var result = await useCase.ExecuteAsync(hostUrl, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetSimulatorRules")
        .WithSummary("Lists all failure simulator rules configured for the authenticated tenant.")
        .Produces<IReadOnlyList<SimulatorRuleResponse>>(StatusCodes.Status200OK);

        // Get Rule by ID
        group.MapGet("/rules/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] GetSimulatorRuleByIdUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var result = await useCase.ExecuteAsync(id, hostUrl, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetSimulatorRuleById")
        .WithSummary("Gets configuration details for a specific failure simulator rule.")
        .Produces<SimulatorRuleResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Update Rule
        group.MapPut("/rules/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateSimulatorRuleCommand command,
            [FromServices] UpdateSimulatorRuleUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var hostUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var result = await useCase.ExecuteAsync(id, command, hostUrl, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("UpdateSimulatorRule")
        .WithSummary("Updates strategy, status codes, failure rate, and response payload for a simulator rule.")
        .Produces<SimulatorRuleResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Delete Rule
        group.MapDelete("/rules/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] DeleteSimulatorRuleUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status204NoContent);
        })
        .WithName("DeleteSimulatorRule")
        .WithSummary("Deletes a failure simulator rule and all its logged execution history.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Reset Sequential Steps
        group.MapPost("/rules/{id:guid}/reset", async (
            [FromRoute] Guid id,
            [FromServices] ResetSimulatorRuleStepsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("ResetSimulatorRuleSteps")
        .WithSummary("Resets the sequential attempt counter for retry backoff testing.")
        .Produces(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Get Executions
        group.MapGet("/executions", async (
            [FromQuery] Guid? ruleId,
            [FromQuery] int? statusCode,
            [FromQuery] string? method,
            [FromQuery] string? search,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromServices] GetSimulatorExecutionsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(
                ruleId,
                statusCode,
                method,
                search,
                page ?? 1,
                pageSize ?? 50,
                cancellationToken
            );
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetSimulatorExecutions")
        .WithSummary("Retrieves logged execution history for simulator invocations.")
        .Produces<PagedSimulatorExecutionsResponse>(StatusCodes.Status200OK);

        // Get Single Execution
        group.MapGet("/executions/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] GetSimulatorExecutionByIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetSimulatorExecutionById")
        .WithSummary("Gets deep inspection details for a simulated execution.")
        .Produces<SimulatorExecutionResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Clear Executions
        group.MapDelete("/executions", async (
            [FromQuery] Guid? ruleId,
            [FromServices] ClearSimulatorExecutionsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(ruleId, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status204NoContent);
        })
        .WithName("ClearSimulatorExecutions")
        .WithSummary("Clears simulator execution history.")
        .Produces(StatusCodes.Status204NoContent);

        // Get Stats
        group.MapGet("/stats", async (
            [FromServices] GetSimulatorStatsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetSimulatorStats")
        .WithSummary("Retrieves aggregated statistics and fault distribution for the failure simulator.")
        .Produces<SimulatorStatsResponse>(StatusCodes.Status200OK);

        // Test Dispatch
        group.MapPost("/test-dispatch", async (
            [FromBody] TestDispatchCommand command,
            [FromServices] TestDispatchSimulatorUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("TestDispatchSimulator")
        .WithSummary("Sends a simulated test dispatch to verify rule behavior directly from the developer UI.")
        .Produces<SimulatedExecutionResult>(StatusCodes.Status200OK);


        // 2. Public Receiver Endpoints (AllowAnonymous)

        // Named Rule Receiver
        app.MapMethods("/api/v1/simulator/receive/{slug}", ReceiverHttpMethods, async (
            [FromRoute] string slug,
            [FromServices] ProcessSimulatorRequestUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var (path, query, headersJson, body, contentType, contentLength, clientIp) = await ExtractRequestContextAsync(httpContext, cancellationToken);

            var result = await useCase.ExecuteAsync(
                slug,
                httpContext.Request.Method,
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
            foreach (var (k, v) in sim.Headers)
            {
                httpContext.Response.Headers[k] = v;
            }

            return Results.Content(
                content: sim.Body ?? string.Empty,
                contentType: sim.ContentType,
                statusCode: sim.StatusCode
            );
        })
        .WithName("ReceiveSimulatorWebhook")
        .WithSummary("Public receiver endpoint executing a configured simulator rule.")
        .AllowAnonymous();

        // Ad-Hoc Status Receiver
        app.MapMethods("/api/v1/simulator/http/{statusCode:int}", ReceiverHttpMethods, async (
            [FromRoute] int statusCode,
            [FromQuery] int? delay,
            [FromQuery] int? retryAfter,
            [FromQuery] string? bodyParam,
            [FromServices] ExecuteAdHocSimulationUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var (path, query, headersJson, body, contentType, contentLength, clientIp) = await ExtractRequestContextAsync(httpContext, cancellationToken);

            var result = await useCase.ExecuteAsync(
                requestedStatusCode: statusCode,
                delayMsParam: delay,
                failureRateParam: null,
                retryAfterParam: retryAfter,
                customBodyParam: bodyParam,
                httpMethod: httpContext.Request.Method,
                path: path,
                queryString: query,
                headersJson: headersJson,
                body: body,
                contentType: contentType,
                contentLength: contentLength,
                clientIp: clientIp,
                cancellationToken: cancellationToken
            );

            var sim = result.Value;
            foreach (var (k, v) in sim.Headers)
            {
                httpContext.Response.Headers[k] = v;
            }

            return Results.Content(
                content: sim.Body ?? string.Empty,
                contentType: sim.ContentType,
                statusCode: sim.StatusCode
            );
        })
        .WithName("ReceiveAdHocStatusWebhook")
        .WithSummary("Public ad-hoc receiver returning specified HTTP status code with optional delay and retry-after headers.")
        .AllowAnonymous();

        // Ad-Hoc Timeout Receiver
        app.MapMethods("/api/v1/simulator/timeout", ReceiverHttpMethods, async (
            [FromQuery] int? delay,
            [FromQuery] int? statusCode,
            [FromServices] ExecuteAdHocSimulationUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var (path, query, headersJson, body, contentType, contentLength, clientIp) = await ExtractRequestContextAsync(httpContext, cancellationToken);

            var result = await useCase.ExecuteAsync(
                requestedStatusCode: statusCode ?? 504,
                delayMsParam: delay ?? 5000,
                failureRateParam: null,
                retryAfterParam: null,
                customBodyParam: null,
                httpMethod: httpContext.Request.Method,
                path: path,
                queryString: query,
                headersJson: headersJson,
                body: body,
                contentType: contentType,
                contentLength: contentLength,
                clientIp: clientIp,
                cancellationToken: cancellationToken
            );

            var sim = result.Value;
            foreach (var (k, v) in sim.Headers)
            {
                httpContext.Response.Headers[k] = v;
            }

            return Results.Content(
                content: sim.Body ?? string.Empty,
                contentType: sim.ContentType,
                statusCode: sim.StatusCode
            );
        })
        .WithName("ReceiveAdHocTimeoutWebhook")
        .WithSummary("Public ad-hoc receiver simulating slow network timeout.")
        .AllowAnonymous();

        // Ad-Hoc Chaos Receiver
        app.MapMethods("/api/v1/simulator/chaos", ReceiverHttpMethods, async (
            [FromQuery] double? failureRate,
            [FromQuery] int? failStatus,
            [FromQuery] int? delay,
            [FromServices] ExecuteAdHocSimulationUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var (path, query, headersJson, body, contentType, contentLength, clientIp) = await ExtractRequestContextAsync(httpContext, cancellationToken);

            var result = await useCase.ExecuteAsync(
                requestedStatusCode: failStatus ?? 500,
                delayMsParam: delay,
                failureRateParam: failureRate ?? 50.0,
                retryAfterParam: null,
                customBodyParam: null,
                httpMethod: httpContext.Request.Method,
                path: path,
                queryString: query,
                headersJson: headersJson,
                body: body,
                contentType: contentType,
                contentLength: contentLength,
                clientIp: clientIp,
                cancellationToken: cancellationToken
            );

            var sim = result.Value;
            foreach (var (k, v) in sim.Headers)
            {
                httpContext.Response.Headers[k] = v;
            }

            return Results.Content(
                content: sim.Body ?? string.Empty,
                contentType: sim.ContentType,
                statusCode: sim.StatusCode
            );
        })
        .WithName("ReceiveAdHocChaosWebhook")
        .WithSummary("Public ad-hoc receiver with configurable chaos failure rate.")
        .AllowAnonymous();

        return app;
    }

    private static async Task<(string Path, string? Query, string HeadersJson, string? Body, string? ContentType, long ContentLength, string? ClientIp)> ExtractRequestContextAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var req = httpContext.Request;
        var path = req.Path.Value ?? string.Empty;
        var query = req.QueryString.HasValue ? req.QueryString.Value : null;

        var headersDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in req.Headers)
        {
            headersDict[k] = v.ToString();
        }
        var headersJson = JsonSerializer.Serialize(headersDict);

        string? body = null;
        if (req.ContentLength > 0 || req.Body != null)
        {
            using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
        var contentType = req.ContentType;
        var contentLength = req.ContentLength ?? (body != null ? Encoding.UTF8.GetByteCount(body) : 0);

        return (path, query, headersJson, body, contentType, contentLength, clientIp);
    }
}
