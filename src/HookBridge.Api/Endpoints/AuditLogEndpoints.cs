using HookBridge.Api.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.AuditLogs;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/audit-logs")
            .WithTags("Audit Logs")
            .RequireAuthorization();

        // 1. Get Audit Logs (Requires TenantAdmin)
        group.MapGet("/", async (
            [AsParameters] AuditLogQuery query,
            [FromServices] GetAuditLogsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(query, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetAuditLogs")
        .WithSummary("Retrieves immutable audit entries for compliance and traceability within the active tenant.")
        .RequireAuthorization(AuthorizationPolicies.RequireTenantAdmin)
        .Produces<PagedList<AuditEntryResponse>>(StatusCodes.Status200OK);

        return app;
    }
}
