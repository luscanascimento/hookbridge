using HookBridge.Domain.Enums;

namespace HookBridge.Application.ControlPlane.DTOs;

public sealed record TraceSpanEventDto(
    string Name,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, string>? Attributes = null);

public sealed record TraceSpanDto(
    string SpanId,
    string? ParentSpanId,
    string Name,
    string Service,
    string Kind,
    DateTimeOffset StartTime,
    long DurationMs,
    long OffsetMs,
    string Status,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<TraceSpanEventDto> Events);

public sealed record TraceRootEventDto(
    Guid? EventId,
    string EventType,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string? IdempotencyKey = null,
    string? PayloadPreview = null);

public sealed record TraceSummaryResponse(
    string TraceId,
    string CorrelationId,
    string? TraceParent,
    string EventType,
    DateTimeOffset InitiatedAt,
    DateTimeOffset? CompletedAt,
    long TotalDurationMs,
    string Status,
    int SpanCount,
    int DeliveryCount,
    int AttemptCount,
    int AuditCount);

public sealed record TraceDetailResponse(
    string TraceId,
    string CorrelationId,
    string? TraceParent,
    string EventType,
    DateTimeOffset InitiatedAt,
    DateTimeOffset? CompletedAt,
    long TotalDurationMs,
    string OverallStatus,
    TraceRootEventDto? RootEvent,
    IReadOnlyList<TraceSpanDto> Spans,
    IReadOnlyList<DeliveryDetailResponse> Deliveries,
    IReadOnlyList<AuditEntryResponse> AuditLogs);

public sealed record TraceQuery(
    string? Query = null,
    string? Status = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int Page = 1,
    int PageSize = 20);
