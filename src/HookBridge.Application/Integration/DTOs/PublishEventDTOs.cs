using System.Text.Json;

namespace HookBridge.Application.Integration.DTOs;

public sealed record PublishEventCommand(
    string EventType,
    JsonElement Payload,
    string? IdempotencyKey = null,
    int? Version = 1,
    string? CorrelationId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record PublishEventResponse(
    Guid EventId,
    string Status,
    string EventType,
    int DeliveriesScheduled,
    DateTimeOffset IngestedAt,
    string? TraceParent,
    string CorrelationId);
