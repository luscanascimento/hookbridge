using HookBridge.Domain.Enums;

namespace HookBridge.Application.ControlPlane.DTOs;

public sealed record DeliveryResponse(
    Guid Id,
    Guid TenantId,
    Guid EventId,
    Guid EndpointId,
    Guid SubscriptionId,
    string EventType,
    DeliveryStatus Status,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? DeliveredAt,
    int AttemptCount,
    string? TraceParent,
    string CorrelationId,
    Guid? OriginalDeliveryId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? EndpointUrl = null);

public sealed record AttemptResponse(
    Guid Id,
    Guid DeliveryId,
    int AttemptNumber,
    int? HttpStatusCode,
    string RequestHeadersJson,
    string RequestBody,
    string? ResponseHeadersJson,
    string? ResponseBody,
    long ElapsedMs,
    string? ErrorMessage,
    DateTimeOffset ExecutedAt);

public sealed record DeliveryDetailResponse(
    Guid Id,
    Guid TenantId,
    Guid EventId,
    Guid EndpointId,
    Guid SubscriptionId,
    string EventType,
    DeliveryStatus Status,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? DeliveredAt,
    int AttemptCount,
    string? TraceParent,
    string CorrelationId,
    Guid? OriginalDeliveryId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<AttemptResponse> Attempts,
    string? EndpointUrl = null);

public sealed record GetDeliveriesQuery(
    Guid? EndpointId = null,
    DeliveryStatus? Status = null,
    string? EventType = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    string? CorrelationId = null,
    int Page = 1,
    int PageSize = 20);

public sealed record TimeSeriesBucket(
    DateTimeOffset Timestamp,
    long Total,
    long Success,
    long Failed,
    long DeadLettered,
    double AvgLatencyMs);

public sealed record DeliveryStatsResponse(
    long TotalDeliveries,
    long SuccessfulDeliveries,
    long FailedDeliveries,
    long PendingDeliveries,
    long DeadLetteredDeliveries,
    double SuccessRatePercentage,
    double AverageLatencyMs,
    IReadOnlyList<TimeSeriesBucket>? TimeSeries = null);

public sealed record RecordDeliveryAttemptCommand(
    int? HttpStatusCode,
    string RequestHeadersJson,
    string RequestBody,
    string? ResponseHeadersJson,
    string? ResponseBody,
    long ElapsedMs,
    string? ErrorMessage,
    DeliveryStatus FinalStatus);

public sealed record ReplayDeliveryCommand(
    Guid? OverrideEndpointId = null);

public sealed record ReplayDeliveryResponse(
    Guid DeliveryId,
    Guid OriginalDeliveryId,
    Guid EndpointId,
    string EventType,
    DeliveryStatus Status,
    DateTimeOffset ScheduledAt,
    string TraceParent,
    string CorrelationId);

public sealed record BulkReplayDeliveriesCommand(
    IReadOnlyList<Guid>? DeliveryIds = null,
    Guid? EndpointId = null,
    DeliveryStatus? Status = null,
    string? EventType = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int? MaxCount = 50);

public sealed record BulkReplayDeliveriesResponse(
    int ReplayedCount,
    IReadOnlyList<ReplayDeliveryResponse> ReplayedDeliveries);

public sealed record DeliveryLineageResponse(
    Guid RootDeliveryId,
    IReadOnlyList<DeliveryResponse> LineageChain);

