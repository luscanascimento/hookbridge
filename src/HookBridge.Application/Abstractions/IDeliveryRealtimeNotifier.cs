using HookBridge.Domain.Entities;

namespace HookBridge.Application.Abstractions;

/// <summary>
/// Abstraction for broadcasting delivery lifecycle events to real-time clients.
/// </summary>
public interface IDeliveryRealtimeNotifier
{
    Task NotifyDeliveryDispatchedAsync(Delivery delivery, CancellationToken cancellationToken = default);

    Task NotifyDeliveryAttemptRecordedAsync(Delivery delivery, Attempt attempt, CancellationToken cancellationToken = default);

    Task NotifyDeliveryReplayedAsync(Delivery delivery, Guid originalDeliveryId, CancellationToken cancellationToken = default);

    Task NotifyBulkDeliveriesReplayedAsync(Guid tenantId, IReadOnlyList<Delivery> replayedDeliveries, CancellationToken cancellationToken = default);
}
