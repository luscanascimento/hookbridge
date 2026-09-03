using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.UseCases.DeadLetter;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class DeadLetterEndpoints
{
    public static IEndpointRouteBuilder MapDeadLetterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dlq")
            .WithTags("DeadLetterQueue")
            .RequireAuthorization();

        // 1. Peek DLQ (Requires TenantAdmin)
        group.MapGet("/", async (
            [FromQuery] int? count,
            [FromServices] PeekDeadLettersUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(count ?? 10, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("PeekDeadLetters")
        .WithSummary("Peeks pending messages currently residing in the Dead Letter Queue without acknowledging them.")
        .RequireAuthorization(AuthorizationPolicies.RequireTenantAdmin)
        .Produces<DeadLetterListResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        // 2. Replay DLQ (Requires TenantAdmin)
        group.MapPost("/replay", async (
            [FromQuery] int? maxCount,
            [FromServices] ReplayDeadLettersUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(maxCount ?? 50, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("ReplayDeadLetters")
        .WithSummary("Replays dead-lettered messages back to the primary topic exchange for reprocessing.")
        .RequireAuthorization(AuthorizationPolicies.RequireTenantAdmin)
        .Produces<DeadLetterReplayResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        // 3. Purge DLQ (Requires TenantAdmin)
        group.MapDelete("/", async (
            [FromServices] PurgeDeadLettersUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("PurgeDeadLetters")
        .WithSummary("Purges all messages currently residing in the Dead Letter Queue.")
        .RequireAuthorization(AuthorizationPolicies.RequireTenantAdmin)
        .Produces<DeadLetterPurgeResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return app;
    }
}
