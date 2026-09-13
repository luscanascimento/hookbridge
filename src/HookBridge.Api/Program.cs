using System.Globalization;
using HookBridge.Api.Endpoints;
using HookBridge.Api.Hubs;
using HookBridge.Api.Middleware;
using HookBridge.Application;
using HookBridge.Application.Abstractions;
using HookBridge.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Core Application & Infrastructure Layers
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Register SignalR & Realtime Delivery Notifier
builder.Services.AddSignalR();
builder.Services.AddSingleton<IDeliveryRealtimeNotifier, DeliveryRealtimeNotifier>();
builder.Services.AddSingleton<ISandboxRealtimeNotifier, SandboxRealtimeNotifier>();
builder.Services.AddSingleton<ISimulatorRealtimeNotifier, SimulatorRealtimeNotifier>();

// 3. Exception Handling & RFC 7807 ProblemDetails
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();

// 4. Rate Limiting Protection
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        var retryAfterSec = 60;
        if (context.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfterSpan))
        {
            retryAfterSec = Math.Max(1, (int)retryAfterSpan.TotalSeconds);
        }
        context.HttpContext.Response.Headers.RetryAfter = retryAfterSec.ToString(CultureInfo.InvariantCulture);

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = $"Rate limit exceeded. Please retry after {retryAfterSec} seconds.",
            Type = "https://tools.ietf.org/html/rfc6585#section-4"
        };
        problem.Extensions["errorCode"] = "RateLimit.Exceeded";
        await context.HttpContext.Response.WriteAsJsonAsync(problem, token);
    };

    options.AddFixedWindowLimiter("auth-policy", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

// 5. OpenAPI 3.1 Documentation
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? throw new InvalidOperationException("CORS AllowedOrigins is not configured.");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// 6. Security & Error Handling Pipeline
app.UseCors();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<TraceContextEnricherMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();

// 7. Authentication & Authorization Pipeline
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

// 6. Map OpenAPI and Developer Portal in Development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("HookBridge Control Plane API");
        options.WithTheme(ScalarTheme.Moon);
    });
}

// 7. Map Endpoints
app.MapHealthEndpoints();
app.MapDiagnosticsEndpoints();
app.MapAuthEndpoints();
app.MapApplicationEndpoints();
app.MapEndpointEndpoints();
app.MapSubscriptionEndpoints();
app.MapWebhookSecretEndpoints();
app.MapApiKeyEndpoints();
app.MapAuditLogEndpoints();
app.MapWebhookSignatureEndpoints();
app.MapEventPublishingEndpoints();
app.MapDeadLetterEndpoints();
app.MapDeliveryEndpoints();
app.MapTraceEndpoints();
app.MapPayloadEndpoints();
app.MapEventSchemaEndpoints();
app.MapDocEndpoints();
app.MapSandboxEndpoints();
app.MapSimulatorEndpoints();
app.MapObservabilityEndpoints();

// 8. Map Real-time SignalR Hubs
app.MapHub<DeliveryHub>("/hubs/deliveries");

app.Run();

// Required for Integration Testing with WebApplicationFactory
public partial class Program { }
