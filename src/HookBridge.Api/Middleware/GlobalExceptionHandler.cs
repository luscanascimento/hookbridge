using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Middleware;

/// <summary>
/// Global exception handler producing RFC 7807 ProblemDetails responses while preventing internal stack trace disclosure.
/// </summary>
public sealed partial class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        LogUnhandledException(_logger, httpContext.Request.Path, traceId, exception);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An internal server error occurred",
            Detail = "An unexpected error occurred while processing your request. Please contact support with the trace identifier.",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = traceId,
                ["timestamp"] = DateTimeOffset.UtcNow
            }
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);

        return true;
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "An unhandled exception occurred during request {Path}. TraceId: {TraceId}")]
    private static partial void LogUnhandledException(ILogger logger, string path, string traceId, Exception ex);
}
