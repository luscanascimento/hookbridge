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

    // --- Standard Semantic Convention Tags ---
    public const string TagTenantId = "tenant.id";
    public const string TagApplicationId = "application.id";
    public const string TagEndpointId = "endpoint.id";
    public const string TagDeliveryId = "delivery.id";
    public const string TagAttemptId = "attempt.id";
    public const string TagEventType = "event.type";
    public const string TagCorrelationId = "correlation.id";
    public const string TagTraceParent = "w3c.traceparent";
    public const string TagHttpStatusCode = "http.status_code";
    public const string TagFaultType = "fault.type";
    public const string TagReplayMode = "replay.mode";
    public const string TagSchemaVersion = "schema.version";

    /// <summary>
    /// OpenTelemetry ActivitySource for creating custom distributed trace spans across the control plane lifecycle.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(DiagnosticSourceName, Version);

    /// <summary>
    /// OpenTelemetry Meter for recording domain, application, and infrastructure metrics.
    /// </summary>
    public static readonly Meter Meter = new(DiagnosticSourceName, Version);

    // --- Core Delivery & Publishing Metric Instruments ---

    public static readonly Counter<long> EventsPublished = Meter.CreateCounter<long>(
        name: "hookbridge.events.published",
        unit: "{event}",
        description: "Total number of domain events published through the HookBridge ingestion gateway");

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

    public static readonly Counter<long> DeliveriesDeadLettered = Meter.CreateCounter<long>(
        name: "hookbridge.deliveries.deadlettered",
        unit: "{delivery}",
        description: "Total number of webhook deliveries routed to the Dead Letter Queue (DLQ)");

    public static readonly Counter<long> ReplaysTriggered = Meter.CreateCounter<long>(
        name: "hookbridge.replays.triggered",
        unit: "{replay}",
        description: "Total number of manual or bulk delivery replays triggered");

    public static readonly Histogram<double> DeliveryLatency = Meter.CreateHistogram<double>(
        name: "hookbridge.delivery.latency",
        unit: "ms",
        description: "Duration of external webhook HTTP dispatch in milliseconds");

    // --- Security & Cryptography Instruments ---

    public static readonly Counter<long> SignaturesGenerated = Meter.CreateCounter<long>(
        name: "hookbridge.signatures.generated",
        unit: "{signature}",
        description: "Total number of HMAC-SHA256 webhook signatures generated");

    public static readonly Counter<long> SignaturesVerified = Meter.CreateCounter<long>(
        name: "hookbridge.signatures.verified",
        unit: "{verification}",
        description: "Total number of HMAC-SHA256 signature verifications evaluated");

    // --- Simulator & Chaos Testing Instruments ---

    public static readonly Counter<long> SimulatorExecutions = Meter.CreateCounter<long>(
        name: "hookbridge.simulator.executions",
        unit: "{execution}",
        description: "Total number of simulated webhook executions processed");

    public static readonly Counter<long> SimulatorFailuresInjected = Meter.CreateCounter<long>(
        name: "hookbridge.simulator.failures_injected",
        unit: "{fault}",
        description: "Total number of controlled chaos faults injected by the simulator");

    // --- Sandbox Instruments ---

    public static readonly Counter<long> SandboxRequestsCaptured = Meter.CreateCounter<long>(
        name: "hookbridge.sandbox.requests_captured",
        unit: "{request}",
        description: "Total number of inbound requests captured by webhook sandboxes");

    // --- Schemas & Contract Governance Instruments ---

    public static readonly Counter<long> SchemasRegistered = Meter.CreateCounter<long>(
        name: "hookbridge.schemas.registered",
        unit: "{schema}",
        description: "Total number of event schemas and schema versions registered");

    public static readonly Counter<long> SchemaValidations = Meter.CreateCounter<long>(
        name: "hookbridge.schema.validations",
        unit: "{validation}",
        description: "Total number of payload validations performed against event schemas");

    public static readonly Counter<long> SchemaDriftDetected = Meter.CreateCounter<long>(
        name: "hookbridge.schema.drift_detected",
        unit: "{drift}",
        description: "Total number of schema contract drift anomalies detected");

    // --- Real-time SignalR & Telemetry Instruments ---

    public static readonly UpDownCounter<long> ActiveSignalRConnections = Meter.CreateUpDownCounter<long>(
        name: "hookbridge.signalr.active_connections",
        unit: "{connection}",
        description: "Current number of active authenticated SignalR live inspection connections");

    public static readonly Counter<long> RealtimeEventsBroadcasted = Meter.CreateCounter<long>(
        name: "hookbridge.signalr.events_broadcasted",
        unit: "{event}",
        description: "Total number of realtime delivery notifications broadcasted over SignalR");

    public static readonly UpDownCounter<long> ActiveCircuitBreakers = Meter.CreateUpDownCounter<long>(
        name: "hookbridge.circuit_breakers.open",
        unit: "{breaker}",
        description: "Current number of open circuit breakers across tenant endpoints");

    // --- Activity Helper Methods ---

    public static Activity? StartActivity(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        string? correlationId = null,
        Guid? tenantId = null)
    {
        var activity = ActivitySource.StartActivity(name, kind);
        if (activity != null)
        {
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                activity.SetTag(TagCorrelationId, correlationId);
                activity.SetBaggage(TagCorrelationId, correlationId);
            }

            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                activity.SetTag(TagTenantId, tenantId.Value.ToString());
                activity.SetBaggage(TagTenantId, tenantId.Value.ToString());
            }
        }
        return activity;
    }
}
