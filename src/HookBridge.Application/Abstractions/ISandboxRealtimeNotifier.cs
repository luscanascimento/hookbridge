using HookBridge.Domain.Entities;

namespace HookBridge.Application.Abstractions;

public interface ISandboxRealtimeNotifier
{
    Task NotifyRequestCapturedAsync(WebhookSandbox sandbox, SandboxRequest request, CancellationToken cancellationToken = default);
    Task NotifyRequestsClearedAsync(Guid tenantId, Guid sandboxId, CancellationToken cancellationToken = default);
}
