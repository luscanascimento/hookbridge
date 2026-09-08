using System.Diagnostics;
using System.Globalization;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Diagnostics;

namespace HookBridge.Application.ControlPlane.UseCases.Observability;

public sealed class GenerateSyntheticTraceUseCase
{
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GenerateSyntheticTraceUseCase(
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider)
    {
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<SyntheticTraceResponse>> ExecuteAsync(
        SyntheticTraceCommand command,
        CancellationToken cancellationToken = default)
    {
        var correlationId = $"corr_syn_{Guid.NewGuid():N}"[..18];
        var tenantId = _tenantContext.TenantId ?? Guid.NewGuid();
        var capturedSpans = new List<CapturedSpanDto>();
        var overallStopwatch = Stopwatch.StartNew();

        string traceId;
        string rootSpanId;

        // 1. Root Span: Gateway Ingest
        using (var rootActivity = HookBridgeDiagnostics.ActivitySource.StartActivity(
            "hookbridge.gateway.ingest",
            ActivityKind.Server))
        {
            if (rootActivity == null)
            {
                traceId = ActivityTraceId.CreateRandom().ToString();
                rootSpanId = ActivitySpanId.CreateRandom().ToString();
            }
            else
            {
                rootActivity.SetTag(HookBridgeDiagnostics.TagTenantId, tenantId.ToString());
                rootActivity.SetTag(HookBridgeDiagnostics.TagEventType, command.EventType);
                rootActivity.SetTag(HookBridgeDiagnostics.TagCorrelationId, correlationId);
                rootActivity.SetBaggage(HookBridgeDiagnostics.TagTenantId, tenantId.ToString());
                rootActivity.SetBaggage(HookBridgeDiagnostics.TagCorrelationId, correlationId);

                traceId = rootActivity.TraceId.ToString();
                rootSpanId = rootActivity.SpanId.ToString();
            }

            // 2. Child Span: Webhook Cryptographic Signing
            using (var signingActivity = HookBridgeDiagnostics.ActivitySource.StartActivity(
                "hookbridge.webhook.signing",
                ActivityKind.Internal))
            {
                signingActivity?.SetTag("signing.algorithm", "HMAC-SHA256");
                signingActivity?.SetTag("signing.version", "v1");
                await Task.Delay(5, cancellationToken);
                signingActivity?.SetStatus(ActivityStatusCode.Ok);
            }

            // 3. Child Span: Outbox Persistence
            using (var dbActivity = HookBridgeDiagnostics.ActivitySource.StartActivity(
                "hookbridge.outbox.persist",
                ActivityKind.Client))
            {
                dbActivity?.SetTag("db.system", "postgresql");
                dbActivity?.SetTag("db.operation", "INSERT");
                dbActivity?.SetTag("db.name", "hookbridge");
                await Task.Delay(10, cancellationToken);
                dbActivity?.SetStatus(ActivityStatusCode.Ok);
            }

            // 4. Child Span: Outbound HTTP Dispatch
            using (var dispatchActivity = HookBridgeDiagnostics.ActivitySource.StartActivity(
                "hookbridge.delivery.dispatch",
                ActivityKind.Producer))
            {
                dispatchActivity?.SetTag("http.target_url", "https://api.merchant.com/webhooks/receiver");
                dispatchActivity?.SetTag("http.method", "POST");

                var delay = Math.Clamp(command.DelayMs, 5, 2000);
                await Task.Delay(delay, cancellationToken);

                if (command.IncludeFailure)
                {
                    dispatchActivity?.SetTag(HookBridgeDiagnostics.TagHttpStatusCode, "500");
                    dispatchActivity?.SetTag("error.type", "InternalServerError");
                    dispatchActivity?.SetStatus(ActivityStatusCode.Error, "Target endpoint responded with HTTP 500");
                }
                else
                {
                    dispatchActivity?.SetTag(HookBridgeDiagnostics.TagHttpStatusCode, "200");
                    dispatchActivity?.SetStatus(ActivityStatusCode.Ok);
                }
            }

            rootActivity?.SetStatus(command.IncludeFailure ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
        }

        overallStopwatch.Stop();

        // Increment telemetry metric counters
        HookBridgeDiagnostics.EventsPublished.Add(1);
        HookBridgeDiagnostics.DeliveriesDispatched.Add(1);
        if (command.IncludeFailure)
        {
            HookBridgeDiagnostics.DeliveriesFailed.Add(1);
        }
        else
        {
            HookBridgeDiagnostics.DeliveriesSucceeded.Add(1);
        }
        HookBridgeDiagnostics.SignaturesGenerated.Add(1);
        HookBridgeDiagnostics.DeliveryLatency.Record(overallStopwatch.Elapsed.TotalMilliseconds);

        var startTime = _dateTimeProvider.UtcNow;
        var totalDuration = overallStopwatch.Elapsed.TotalMilliseconds;

        // Build representative response spans tree
        capturedSpans.Add(new CapturedSpanDto(
            TraceId: traceId,
            SpanId: rootSpanId,
            ParentSpanId: null,
            OperationName: "hookbridge.gateway.ingest",
            SourceName: HookBridgeDiagnostics.DiagnosticSourceName,
            DurationMs: totalDuration,
            StartTime: startTime,
            Status: command.IncludeFailure ? "Error" : "Ok",
            Tags: new Dictionary<string, string>
            {
                [HookBridgeDiagnostics.TagTenantId] = tenantId.ToString(),
                [HookBridgeDiagnostics.TagEventType] = command.EventType,
                [HookBridgeDiagnostics.TagCorrelationId] = correlationId
            },
            Baggage: new Dictionary<string, string>
            {
                [HookBridgeDiagnostics.TagTenantId] = tenantId.ToString(),
                [HookBridgeDiagnostics.TagCorrelationId] = correlationId
            }
        ));

        capturedSpans.Add(new CapturedSpanDto(
            TraceId: traceId,
            SpanId: ActivitySpanId.CreateRandom().ToString(),
            ParentSpanId: rootSpanId,
            OperationName: "hookbridge.webhook.signing",
            SourceName: HookBridgeDiagnostics.DiagnosticSourceName,
            DurationMs: 5.2,
            StartTime: startTime.AddMilliseconds(2),
            Status: "Ok",
            Tags: new Dictionary<string, string>
            {
                ["signing.algorithm"] = "HMAC-SHA256",
                ["signing.version"] = "v1"
            },
            Baggage: new Dictionary<string, string>()
        ));

        capturedSpans.Add(new CapturedSpanDto(
            TraceId: traceId,
            SpanId: ActivitySpanId.CreateRandom().ToString(),
            ParentSpanId: rootSpanId,
            OperationName: "hookbridge.outbox.persist",
            SourceName: HookBridgeDiagnostics.DiagnosticSourceName,
            DurationMs: 9.8,
            StartTime: startTime.AddMilliseconds(8),
            Status: "Ok",
            Tags: new Dictionary<string, string>
            {
                ["db.system"] = "postgresql",
                ["db.operation"] = "INSERT"
            },
            Baggage: new Dictionary<string, string>()
        ));

        capturedSpans.Add(new CapturedSpanDto(
            TraceId: traceId,
            SpanId: ActivitySpanId.CreateRandom().ToString(),
            ParentSpanId: rootSpanId,
            OperationName: "hookbridge.delivery.dispatch",
            SourceName: HookBridgeDiagnostics.DiagnosticSourceName,
            DurationMs: Math.Max(10, command.DelayMs),
            StartTime: startTime.AddMilliseconds(18),
            Status: command.IncludeFailure ? "Error" : "Ok",
            Tags: new Dictionary<string, string>
            {
                ["http.target_url"] = "https://api.merchant.com/webhooks/receiver",
                [HookBridgeDiagnostics.TagHttpStatusCode] = command.IncludeFailure ? "500" : "200"
            },
            Baggage: new Dictionary<string, string>()
        ));

        return Result.Success(new SyntheticTraceResponse(
            TraceId: traceId,
            RootSpanId: rootSpanId,
            TotalSpansGenerated: capturedSpans.Count,
            TotalDurationMs: totalDuration,
            Spans: capturedSpans
        ));
    }
}
