using HookBridge.Application.Abstractions;
using HookBridge.Domain.Entities;

namespace HookBridge.Application.Common;

/// <summary>
/// No-op implementation of IDeliveryRealtimeNotifier for testing and default fallback.
/// </summary>
public sealed class NullDeliveryRealtimeNotifier : IDeliveryRealtimeNotifier
{
    public static readonly NullDeliveryRealtimeNotifier Instance = new();

    public Task NotifyDeliveryDispatchedAsync(Delivery delivery, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyDeliveryAttemptRecordedAsync(Delivery delivery, Attempt attempt, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyDeliveryReplayedAsync(Delivery delivery, Guid originalDeliveryId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyBulkDeliveriesReplayedAsync(Guid tenantId, IReadOnlyList<Delivery> replayedDeliveries, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
