namespace HookBridge.Application.ControlPlane.UseCases.Observability;

public sealed record ObservabilitySummaryResponse(
    string ServiceName,
    string ServiceVersion,
    string Environment,
    bool OtlpExporterConfigured,
    string? OtlpEndpoint,
    int RegisteredInstrumentsCount,
    int TotalRecordedSpansCount,
    double ProcessWorkingSetMb,
    double ProcessHeapMb,
    int GcGen0Collections,
    int GcGen1Collections,
    int GcGen2Collections,
    IReadOnlyDictionary<string, long> MetricCounters,
    DateTimeOffset Timestamp
);

public sealed record MetricInstrumentDto(
    string Name,
    string Unit,
    string Description,
    string Type,
    long CurrentValue
);

public sealed record CapturedSpanDto(
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string OperationName,
    string SourceName,
    double DurationMs,
    DateTimeOffset StartTime,
    string Status,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyDictionary<string, string> Baggage
);

public sealed record SyntheticTraceCommand(
    string EventType = "order.completed",
    bool IncludeFailure = false,
    int DelayMs = 30
);

public sealed record SyntheticTraceResponse(
    string TraceId,
    string RootSpanId,
    int TotalSpansGenerated,
    double TotalDurationMs,
    IReadOnlyList<CapturedSpanDto> Spans
);
