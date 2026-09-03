using System.Text.Json;

namespace HookBridge.Application.Integration.DTOs;

public sealed record EventFlowIngestRequest(
    Guid? EventId,
    string EventType,
    int? Version,
    string Source,
    DateTimeOffset? OccurredAt,
    string? CorrelationId,
    string? TraceParent,
    string TenantId,
    string IdempotencyKey,
    JsonElement Payload,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record EventFlowIngestResponse(
    Guid EventId,
    string Status,
    DateTimeOffset IngestedAt);

public sealed record DeadLetterMessageDto(
    string MessageId,
    string Exchange,
    string RoutingKey,
    string Queue,
    string TenantId,
    string EventType,
    string Payload,
    string? TraceParent,
    string? CorrelationId,
    string? Reason,
    int AttemptCount,
    DateTimeOffset EnqueuedAt);

public sealed record DeadLetterListResponse(
    int Count,
    IReadOnlyList<DeadLetterMessageDto> Messages);

public sealed record DeadLetterReplayResponse(
    int ReplayedCount,
    string Status);

public sealed record DeadLetterPurgeResponse(
    int PurgedCount,
    string Status);
