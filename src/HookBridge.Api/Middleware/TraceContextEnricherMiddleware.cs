using System.Diagnostics;
using HookBridge.Domain.Diagnostics;

namespace HookBridge.Api.Middleware;

public sealed class TraceContextEnricherMiddleware
{
    private readonly RequestDelegate _next;

    public TraceContextEnricherMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var currentActivity = Activity.Current;
        var traceId = currentActivity?.TraceId.ToString() ?? ActivityTraceId.CreateRandom().ToString();

        // Extract or generate Correlation ID
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? context.Request.Headers["X-Request-Id"].FirstOrDefault()
            ?? $"corr_{Guid.NewGuid():N}"[..18];

        if (currentActivity != null)
        {
            currentActivity.SetTag(HookBridgeDiagnostics.TagCorrelationId, correlationId);
            currentActivity.SetBaggage(HookBridgeDiagnostics.TagCorrelationId, correlationId);

            if (context.Request.Headers.TryGetValue("traceparent", out var traceparent))
            {
                currentActivity.SetTag(HookBridgeDiagnostics.TagTraceParent, traceparent.ToString());
            }
        }

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("X-Trace-Id"))
            {
                context.Response.Headers["X-Trace-Id"] = traceId;
            }

            if (!context.Response.Headers.ContainsKey("X-Correlation-Id"))
            {
                context.Response.Headers["X-Correlation-Id"] = correlationId;
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
