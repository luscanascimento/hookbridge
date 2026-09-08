using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.UseCases.Sandbox;
using HookBridge.Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace HookBridge.Api.Hubs;

public sealed partial class SandboxRealtimeNotifier : ISandboxRealtimeNotifier
{
    private readonly IHubContext<DeliveryHub, IDeliveryHubClient> _hubContext;
    private readonly ILogger<SandboxRealtimeNotifier> _logger;

    public SandboxRealtimeNotifier(
        IHubContext<DeliveryHub, IDeliveryHubClient> hubContext,
        ILogger<SandboxRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    [LoggerMessage(EventId = 3010, Level = LogLevel.Information, Message = "Broadcasting SandboxRequestCaptured for sandbox {Slug} to {TenantGroup}.")]
    private static partial void LogRequestCaptured(ILogger logger, string slug, string tenantGroup);

    [LoggerMessage(EventId = 3011, Level = LogLevel.Information, Message = "Broadcasting SandboxRequestsCleared for sandbox {SandboxId} to {TenantGroup}.")]
    private static partial void LogRequestsCleared(ILogger logger, Guid sandboxId, string tenantGroup);

    public async Task NotifyRequestCapturedAsync(WebhookSandbox sandbox, SandboxRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(request);

        var requestDto = new SandboxRequestResponse(
            request.Id,
            request.SandboxId,
            request.HttpMethod,
            request.Path,
            request.QueryString,
            request.HeadersJson,
            request.Body,
            request.ContentType,
            request.ContentLength,
            request.ClientIp,
            request.ResponseStatusCode,
            request.ResponseDelayMs,
            request.ReceivedAt,
            request.DurationMs
        );

        var evt = new RealtimeSandboxEvent(
            EventType: "SandboxRequestCaptured",
            TenantId: sandbox.TenantId,
            SandboxId: sandbox.Id,
            SandboxSlug: sandbox.Slug,
            Request: requestDto,
            Timestamp: request.ReceivedAt
        );

        var tenantGroup = DeliveryHub.GetTenantGroup(sandbox.TenantId);

        LogRequestCaptured(_logger, sandbox.Slug, tenantGroup);

        await _hubContext.Clients.Group(tenantGroup).ReceiveSandboxRequest(evt);
    }

    public async Task NotifyRequestsClearedAsync(Guid tenantId, Guid sandboxId, CancellationToken cancellationToken = default)
    {
        var tenantGroup = DeliveryHub.GetTenantGroup(tenantId);
        LogRequestsCleared(_logger, sandboxId, tenantGroup);

        await _hubContext.Clients.Group(tenantGroup).SandboxRequestsCleared(sandboxId);
    }
}
