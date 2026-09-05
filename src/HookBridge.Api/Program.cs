using HookBridge.Api.Endpoints;
using HookBridge.Api.Hubs;
using HookBridge.Api.Middleware;
using HookBridge.Application;
using HookBridge.Application.Abstractions;
using HookBridge.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Core Application & Infrastructure Layers
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Register SignalR & Realtime Delivery Notifier
builder.Services.AddSignalR();
builder.Services.AddSingleton<IDeliveryRealtimeNotifier, DeliveryRealtimeNotifier>();

// 3. Exception Handling & RFC 7807 ProblemDetails
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// 4. OpenAPI 3.1 Documentation
builder.Services.AddOpenApi();

var app = builder.Build();

// 4. Security & Error Handling Pipeline
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

// 5. Authentication & Authorization Pipeline
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

// 8. Map Real-time SignalR Hubs
app.MapHub<DeliveryHub>("/hubs/deliveries");

app.Run();

// Required for Integration Testing with WebApplicationFactory
public partial class Program { }
