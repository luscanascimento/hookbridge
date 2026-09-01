using System.Diagnostics;
using System.Reflection;
using HookBridge.Domain.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HookBridge.Api.Endpoints;

public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/diagnostics")
            .WithTags("Diagnostics");

        group.MapGet("/info", (IHostEnvironment env) =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? HookBridgeDiagnostics.Version;

            return Results.Ok(new
            {
                Service = HookBridgeDiagnostics.ServiceName,
                Version = version,
                Environment = env.EnvironmentName,
                ProcessId = Environment.ProcessId,
                ActivityId = Activity.Current?.Id,
                Timestamp = DateTimeOffset.UtcNow
            });
        })
        .WithName("GetDiagnosticsInfo")
        .WithSummary("Returns diagnostic operational metadata about the current HookBridge instance.");

        return app;
    }
}
