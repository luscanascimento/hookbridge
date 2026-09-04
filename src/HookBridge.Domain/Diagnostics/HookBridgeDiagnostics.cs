using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HookBridge.Domain.Diagnostics;

/// <summary>
/// Centralized diagnostic source definitions for OpenTelemetry distributed tracing and metrics in HookBridge.
/// </summary>
public static class HookBridgeDiagnostics
{
    public const string ServiceName = "HookBridge";
    public const string DiagnosticSourceName = "HookBridge.ControlPlane";
    public const string Version = "1.0.0";

    /// <summary>
    /// OpenTelemetry ActivitySource for creating custom distributed trace spans across the control plane lifecycle.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(DiagnosticSourceName, Version);

    /// <summary>
    /// OpenTelemetry Meter for recording domain, application, and infrastructure metrics.
    /// </summary>
    public static readonly Meter Meter = new(DiagnosticSourceName, Version);

    // --- Metric Instruments ---

    public static readonly Counter<long> DeliveriesDispatched = Meter.CreateCounter<long>(
        name: "hookbridge.deliveries.dispatched",
        unit: "{delivery}",
        description: "Total number of webhook deliveries dispatched to target endpoints");

    public static readonly Counter<long> DeliveriesSucceeded = Meter.CreateCounter<long>(
        name: "hookbridge.deliveries.succeeded",
        unit: "{delivery}",
        description: "Total number of webhook deliveries successfully acknowledged by targets");

    public static readonly Counter<long> DeliveriesFailed = Meter.CreateCounter<long>(
        name: "hookbridge.deliveries.failed",
        unit: "{delivery}",
        description: "Total number of webhook deliveries that failed");

    public static readonly Counter<long> ReplaysTriggered = Meter.CreateCounter<long>(
        name: "hookbridge.replays.triggered",
        unit: "{replay}",
        description: "Total number of manual or bulk delivery replays triggered");

    public static readonly Histogram<double> DeliveryLatency = Meter.CreateHistogram<double>(
        name: "hookbridge.delivery.latency",
        unit: "ms",
        description: "Duration of external webhook HTTP dispatch in milliseconds");

    public static readonly UpDownCounter<long> ActiveSignalRConnections = Meter.CreateUpDownCounter<long>(
        name: "hookbridge.signalr.active_connections",
        unit: "{connection}",
        description: "Current number of active authenticated SignalR live inspection connections");

    public static readonly Counter<long> RealtimeEventsBroadcasted = Meter.CreateCounter<long>(
        name: "hookbridge.signalr.events_broadcasted",
        unit: "{event}",
        description: "Total number of realtime delivery notifications broadcasted over SignalR");
}
