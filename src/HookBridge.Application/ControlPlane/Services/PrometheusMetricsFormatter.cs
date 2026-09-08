using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace HookBridge.Application.ControlPlane.Services;

public static class PrometheusMetricsFormatter
{
    public static string FormatMetrics(
        long deliveriesDispatched,
        long deliveriesSucceeded,
        long deliveriesFailed,
        long deliveriesDeadLettered,
        long replaysTriggered,
        long eventsPublished,
        long signaturesGenerated,
        long signaturesVerified,
        long simulatorExecutions,
        long simulatorFailuresInjected,
        long sandboxRequestsCaptured,
        long schemaValidations,
        long schemaDriftDetected,
        long activeSignalRConnections,
        long realtimeEventsBroadcasted,
        long activeCircuitBreakers)
    {
        var sb = new StringBuilder();
        var process = Process.GetCurrentProcess();

        // Process & Runtime Metrics
        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP process_working_set_bytes Process physical memory consumption in bytes.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE process_working_set_bytes gauge");
        sb.AppendLine(CultureInfo.InvariantCulture, $"process_working_set_bytes {process.WorkingSet64}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP process_heap_bytes Process managed heap memory size in bytes.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE process_heap_bytes gauge");
        sb.AppendLine(CultureInfo.InvariantCulture, $"process_heap_bytes {GC.GetTotalMemory(false)}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP dotnet_gc_collections_total Total number of garbage collections by generation.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE dotnet_gc_collections_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"dotnet_gc_collections_total{{generation=\"0\"}} {GC.CollectionCount(0)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"dotnet_gc_collections_total{{generation=\"1\"}} {GC.CollectionCount(1)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"dotnet_gc_collections_total{{generation=\"2\"}} {GC.CollectionCount(2)}");
        sb.AppendLine();

        // HookBridge Delivery & Gateway Metrics
        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_events_published_total Total domain events published through ingestion gateway.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_events_published_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_events_published_total {eventsPublished}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_deliveries_dispatched_total Total webhook deliveries dispatched to target endpoints.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_deliveries_dispatched_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_deliveries_dispatched_total {deliveriesDispatched}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_deliveries_succeeded_total Total webhook deliveries acknowledged with success.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_deliveries_succeeded_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_deliveries_succeeded_total {deliveriesSucceeded}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_deliveries_failed_total Total webhook deliveries that failed.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_deliveries_failed_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_deliveries_failed_total {deliveriesFailed}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_deliveries_deadlettered_total Total webhook deliveries routed to DLQ.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_deliveries_deadlettered_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_deliveries_deadlettered_total {deliveriesDeadLettered}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_replays_triggered_total Total manual or bulk delivery replays triggered.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_replays_triggered_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_replays_triggered_total {replaysTriggered}");
        sb.AppendLine();

        // Security & Cryptography Metrics
        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_signatures_generated_total Total HMAC-SHA256 webhook signatures generated.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_signatures_generated_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_signatures_generated_total {signaturesGenerated}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_signatures_verified_total Total HMAC-SHA256 webhook signatures verified.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_signatures_verified_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_signatures_verified_total {signaturesVerified}");
        sb.AppendLine();

        // Simulator & Sandbox Metrics
        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_simulator_executions_total Total simulated webhook executions processed.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_simulator_executions_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_simulator_executions_total {simulatorExecutions}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_simulator_failures_injected_total Total chaos faults injected by simulator.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_simulator_failures_injected_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_simulator_failures_injected_total {simulatorFailuresInjected}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_sandbox_requests_captured_total Total inbound requests captured by sandboxes.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_sandbox_requests_captured_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_sandbox_requests_captured_total {sandboxRequestsCaptured}");
        sb.AppendLine();

        // Schemas Metrics
        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_schema_validations_total Total event schema validations performed.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_schema_validations_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_schema_validations_total {schemaValidations}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_schema_drift_detected_total Total contract drift anomalies detected.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_schema_drift_detected_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_schema_drift_detected_total {schemaDriftDetected}");
        sb.AppendLine();

        // Real-time & Circuit Breakers
        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_signalr_active_connections Current active SignalR client connections.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_signalr_active_connections gauge");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_signalr_active_connections {activeSignalRConnections}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_signalr_events_broadcasted_total Total real-time events broadcasted.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_signalr_events_broadcasted_total counter");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_signalr_events_broadcasted_total {realtimeEventsBroadcasted}");
        sb.AppendLine();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# HELP hookbridge_circuit_breakers_open Current number of open circuit breakers.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# TYPE hookbridge_circuit_breakers_open gauge");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hookbridge_circuit_breakers_open {activeCircuitBreakers}");

        return sb.ToString();
    }
}
