using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;

namespace HookBridge.Application.Abstractions;

/// <summary>
/// HTTP / AMQP client for integrating HookBridge (Control Plane) with EventFlow (Data Plane).
/// Handles transactional event ingestion and DLQ management.
/// </summary>
public interface IEventFlowClient
{
    /// <summary>
    /// Forwards an ingested event to the EventFlow Data Plane transactional outbox.
    /// </summary>
    Task<Result<EventFlowIngestResponse>> IngestEventAsync(EventFlowIngestRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Peeks unacknowledged messages currently residing in the EventFlow Dead Letter Queue.
    /// </summary>
    Task<Result<IReadOnlyList<DeadLetterMessageDto>>> PeekDlqAsync(int count = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replays messages from the EventFlow DLQ back to the primary topic exchange.
    /// </summary>
    Task<Result<int>> ReplayDlqAsync(int maxCount = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges all messages from the EventFlow DLQ.
    /// </summary>
    Task<Result<int>> PurgeDlqAsync(CancellationToken cancellationToken = default);
}
