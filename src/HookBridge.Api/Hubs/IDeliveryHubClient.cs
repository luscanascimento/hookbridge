using HookBridge.Application.ControlPlane.DTOs;

namespace HookBridge.Api.Hubs;

/// <summary>
/// Strongly-typed client contract for HookBridge SignalR live delivery monitoring.
/// </summary>
public interface IDeliveryHubClient
{
    /// <summary>
    /// Generic delivery event handler covering all lifecycle transitions.
    /// </summary>
    Task ReceiveDeliveryEvent(RealtimeDeliveryEvent deliveryEvent);

    /// <summary>
    /// Invoked when a new delivery has been created and scheduled/dispatched.
    /// </summary>
    Task DeliveryDispatched(RealtimeDeliveryEvent deliveryEvent);

    /// <summary>
    /// Invoked when an outbound delivery attempt has executed with status/latency/response.
    /// </summary>
    Task DeliveryAttemptRecorded(RealtimeDeliveryEvent deliveryEvent);

    /// <summary>
    /// Invoked when a delivery has been replayed.
    /// </summary>
    Task DeliveryReplayed(RealtimeDeliveryEvent deliveryEvent);

    /// <summary>
    /// Invoked when a batch of deliveries have been replayed.
    /// </summary>
    Task BulkDeliveriesReplayed(IReadOnlyList<RealtimeDeliveryEvent> deliveryEvents);

    /// <summary>
    /// Invoked when an incoming request is captured by a webhook sandbox receiver.
    /// </summary>
    Task ReceiveSandboxRequest(HookBridge.Application.ControlPlane.UseCases.Sandbox.RealtimeSandboxEvent sandboxEvent);

    /// <summary>
    /// Invoked when sandbox request history is cleared.
    /// </summary>
    Task SandboxRequestsCleared(Guid sandboxId);

    /// <summary>
    /// Invoked when an incoming request is captured by a delivery failure simulator rule or ad-hoc mock.
    /// </summary>
    Task ReceiveSimulatorExecution(HookBridge.Application.ControlPlane.UseCases.Simulator.RealtimeSimulatorEvent simulatorEvent);

    /// <summary>
    /// Invoked when simulator execution history is cleared.
    /// </summary>
    Task SimulatorExecutionsCleared(Guid? ruleId);
}
