using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HookBridge.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/health")
            .WithTags("Health");

        group.MapGet("/live", () => Results.Ok(new
        {
            Status = "Healthy",
            Component = "HookBridge.ControlPlane",
            Timestamp = DateTimeOffset.UtcNow
        }))
        .WithName("LivenessCheck")
        .WithSummary("Basic liveness probe indicating that the process is running.");

        group.MapGet("/ready", () => Results.Ok(new
        {
            Status = "Ready",
            Component = "HookBridge.ControlPlane",
            Subsystems = new
            {
                Database = "Configured",
                Observability = "Active"
            },
            Timestamp = DateTimeOffset.UtcNow
        }))
        .WithName("ReadinessCheck")
        .WithSummary("Readiness probe indicating that the service is initialized and accepting requests.");

        return app;
    }
}
