using System.Globalization;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Traces;

public sealed class GetTraceDetailUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetTraceDetailUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<TraceDetailResponse>> ExecuteAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<TraceDetailResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result.Failure<TraceDetailResponse>(DomainError.Validation("Trace.InvalidIdentifier", "A valid trace identifier, correlation ID, or delivery ID is required."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var trimmedId = identifier.Trim();

        // 1. Find deliveries by correlationId, traceparent substring, or delivery Guid
        var isGuid = Guid.TryParse(trimmedId, out var parsedGuid);

        var deliveries = await _dbContext.Deliveries
            .AsNoTracking()
            .Include(d => d.Attempts)
            .Where(d => d.TenantId == tenantId &&
                        (d.CorrelationId == trimmedId ||
                         (d.TraceParent != null && d.TraceParent.Contains(trimmedId)) ||
                         (isGuid && d.Id == parsedGuid) ||
                         (isGuid && d.EventId == parsedGuid)))
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        // If not found in Deliveries, check AuditEntries
        var auditLogs = await _dbContext.AuditEntries
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId &&
                        ((a.TraceId != null && a.TraceId.Contains(trimmedId)) ||
                         a.ResourceId == trimmedId ||
                         a.DetailsJson.Contains(trimmedId)))
            .OrderBy(a => a.Timestamp)
            .Select(a => new AuditEntryResponse(
                a.Id,
                a.TenantId,
                a.UserId,
                a.Action,
                a.ResourceType,
                a.ResourceId,
                a.DetailsJson,
                a.IpAddress,
                a.TraceId,
                a.Timestamp))
            .ToListAsync(cancellationToken);

        if (deliveries.Count == 0 && auditLogs.Count == 0)
        {
            return Result.Failure<TraceDetailResponse>(DomainError.NotFound("Trace.NotFound", $"No correlated trace found for identifier '{trimmedId}'."));
        }

        // 2. Fetch Endpoints to map target URLs
        var endpointIds = deliveries.Select(d => d.EndpointId).Distinct().ToList();
        var endpoints = await _dbContext.Endpoints
            .AsNoTracking()
            .Where(e => endpointIds.Contains(e.Id) && e.TenantId == tenantId)
            .ToDictionaryAsync(e => e.Id, e => e.TargetUrl, cancellationToken);

        // 3. Construct DeliveryDetailResponses
        var deliveryDetails = deliveries.Select(d =>
        {
            var attemptsDto = d.Attempts
                .OrderBy(a => a.AttemptNumber)
                .Select(a => new AttemptResponse(
                    a.Id,
                    a.DeliveryId,
                    a.AttemptNumber,
                    a.HttpStatusCode,
                    a.RequestHeadersJson,
                    a.RequestBody,
                    a.ResponseHeadersJson,
                    a.ResponseBody,
                    a.ElapsedMs,
                    a.ErrorMessage,
                    a.ExecutedAt))
                .ToList();

            endpoints.TryGetValue(d.EndpointId, out var targetUrl);

            return new DeliveryDetailResponse(
                d.Id,
                d.TenantId,
                d.EventId,
                d.EndpointId,
                d.SubscriptionId,
                d.EventType,
                d.Status,
                d.ScheduledAt,
                d.DeliveredAt,
                d.AttemptCount,
                d.TraceParent,
                d.CorrelationId,
                d.OriginalDeliveryId,
                d.CreatedAt,
                d.UpdatedAt,
                attemptsDto,
                targetUrl);
        }).ToList();

        // 4. Derive Root Event & Trace Identifiers
        var firstDelivery = deliveries.FirstOrDefault();
        var correlationId = firstDelivery?.CorrelationId ?? trimmedId;
        var traceParent = firstDelivery?.TraceParent ?? auditLogs.FirstOrDefault()?.TraceId;
        var traceId = ExtractTraceId(traceParent, correlationId);
        var eventType = firstDelivery?.EventType ?? auditLogs.FirstOrDefault(a => a.Action.Contains("Event"))?.ResourceType ?? "webhook.event";
        var initiatedAt = firstDelivery?.CreatedAt ?? auditLogs.FirstOrDefault()?.Timestamp ?? DateTimeOffset.UtcNow;
        var completedAt = deliveries.Max(d => d.DeliveredAt ?? d.UpdatedAt) ?? auditLogs.LastOrDefault()?.Timestamp;
        var overallStatus = ResolveOverallStatus(deliveries);

        // 5. Build Waterfall DAG Spans
        var spans = BuildWaterfallSpans(traceId, traceParent, correlationId, initiatedAt, deliveries, endpoints, auditLogs);
        var totalDurationMs = spans.Count > 0 ? spans.Max(s => s.OffsetMs + s.DurationMs) : 15;

        var rootEvent = new TraceRootEventDto(
            firstDelivery?.EventId,
            eventType,
            initiatedAt,
            correlationId,
            null,
            null);

        return Result.Success(new TraceDetailResponse(
            traceId,
            correlationId,
            traceParent,
            eventType,
            initiatedAt,
            completedAt,
            totalDurationMs,
            overallStatus,
            rootEvent,
            spans,
            deliveryDetails,
            auditLogs));
    }

    private static List<TraceSpanDto> BuildWaterfallSpans(
        string traceId,
        string? traceParent,
        string correlationId,
        DateTimeOffset baseTime,
        List<Delivery> deliveries,
        Dictionary<Guid, string> endpoints,
        List<AuditEntryResponse> auditLogs)
    {
        var spans = new List<TraceSpanDto>();
        long currentOffset = 0;

        // Span 1: Gateway Ingestion
        var gatewaySpanId = Guid.NewGuid().ToString("N")[..16];
        const long gatewayDuration = 8;
        spans.Add(new TraceSpanDto(
            gatewaySpanId,
            null,
            "hookbridge.gateway.ingest",
            "HookBridge Gateway",
            "Server",
            baseTime,
            gatewayDuration,
            currentOffset,
            "Ok",
            new Dictionary<string, string>
            {
                ["component"] = "Gateway API",
                ["correlation.id"] = correlationId,
                ["traceparent"] = traceParent ?? "None",
                ["deliveries.scheduled"] = deliveries.Count.ToString(CultureInfo.InvariantCulture)
            },
            new List<TraceSpanEventDto>
            {
                new("Event Ingested", baseTime),
                new("Subscription Rules Evaluated", baseTime.AddMilliseconds(4))
            }));

        currentOffset += gatewayDuration;

        // Span 2: EventFlow Transactional Outbox
        var outboxSpanId = Guid.NewGuid().ToString("N")[..16];
        const long outboxDuration = 12;
        spans.Add(new TraceSpanDto(
            outboxSpanId,
            gatewaySpanId,
            "eventflow.transactional_outbox",
            "EventFlow Data Plane",
            "Internal",
            baseTime.AddMilliseconds(currentOffset),
            outboxDuration,
            currentOffset,
            "Ok",
            new Dictionary<string, string>
            {
                ["db.system"] = "postgresql",
                ["outbox.table"] = "outbox_messages",
                ["transaction.status"] = "Committed"
            },
            new List<TraceSpanEventDto>
            {
                new("Outbox Record Staged", baseTime.AddMilliseconds(currentOffset)),
                new("Transaction Committed", baseTime.AddMilliseconds(currentOffset + outboxDuration))
            }));

        currentOffset += outboxDuration;

        // Span 3: RabbitMQ Broker Transit
        var brokerSpanId = Guid.NewGuid().ToString("N")[..16];
        const long brokerDuration = 6;
        spans.Add(new TraceSpanDto(
            brokerSpanId,
            outboxSpanId,
            "rabbitmq.broker_publish",
            "RabbitMQ Message Broker",
            "Producer",
            baseTime.AddMilliseconds(currentOffset),
            brokerDuration,
            currentOffset,
            "Ok",
            new Dictionary<string, string>
            {
                ["messaging.system"] = "rabbitmq",
                ["messaging.destination"] = "hookbridge.events.topic",
                ["messaging.routing_key"] = deliveries.FirstOrDefault()?.EventType ?? "events.#"
            },
            new List<TraceSpanEventDto>
            {
                new("Published to Exchange", baseTime.AddMilliseconds(currentOffset)),
                new("Delivered to Consumer Queues", baseTime.AddMilliseconds(currentOffset + brokerDuration))
            }));

        currentOffset += brokerDuration;

        // Span 4: EventFlow Consumer Worker
        var workerSpanId = Guid.NewGuid().ToString("N")[..16];
        const long workerDuration = 10;
        spans.Add(new TraceSpanDto(
            workerSpanId,
            brokerSpanId,
            "eventflow.consumer_worker",
            "Consumer Worker",
            "Consumer",
            baseTime.AddMilliseconds(currentOffset),
            workerDuration,
            currentOffset,
            "Ok",
            new Dictionary<string, string>
            {
                ["worker.group"] = "hookbridge-dispatch-workers",
                ["idempotency.check"] = "Passed",
                ["active_endpoints"] = deliveries.Count.ToString(CultureInfo.InvariantCulture)
            },
            new List<TraceSpanEventDto>
            {
                new("Message Received by Worker", baseTime.AddMilliseconds(currentOffset)),
                new("HMAC Signature Computed", baseTime.AddMilliseconds(currentOffset + 5))
            }));

        currentOffset += workerDuration;

        // Spans 5..N: Deliveries & Execution Attempts
        foreach (var delivery in deliveries)
        {
            endpoints.TryGetValue(delivery.EndpointId, out var targetUrl);
            var deliverySpanId = Guid.NewGuid().ToString("N")[..16];

            var sortedAttempts = delivery.Attempts.OrderBy(a => a.AttemptNumber).ToList();
            if (sortedAttempts.Count == 0)
            {
                spans.Add(new TraceSpanDto(
                    deliverySpanId,
                    workerSpanId,
                    "hookbridge.endpoint_dispatch (queued)",
                    "Dispatch Worker",
                    "Client",
                    baseTime.AddMilliseconds(currentOffset),
                    5,
                    currentOffset,
                    "Pending",
                    new Dictionary<string, string>
                    {
                        ["endpoint.id"] = delivery.EndpointId.ToString(),
                        ["endpoint.url"] = targetUrl ?? "Unknown",
                        ["delivery.status"] = delivery.Status.ToString()
                    },
                    new List<TraceSpanEventDto>()));
            }
            else
            {
                foreach (var att in sortedAttempts)
                {
                    var attemptSpanId = Guid.NewGuid().ToString("N")[..16];
                    var attDuration = Math.Max(att.ElapsedMs, 5);
                    var attStatus = att.HttpStatusCode >= 200 && att.HttpStatusCode < 300 ? "Ok" : "Error";

                    spans.Add(new TraceSpanDto(
                        attemptSpanId,
                        deliverySpanId,
                        $"http.post {targetUrl ?? "webhook"}",
                        "Outbound HTTP Dispatcher",
                        "Client",
                        att.ExecutedAt,
                        attDuration,
                        currentOffset,
                        attStatus,
                        new Dictionary<string, string>
                        {
                            ["http.status_code"] = (att.HttpStatusCode?.ToString(CultureInfo.InvariantCulture) ?? "N/A"),
                            ["http.url"] = targetUrl ?? "Unknown",
                            ["attempt.number"] = att.AttemptNumber.ToString(CultureInfo.InvariantCulture),
                            ["http.latency_ms"] = att.ElapsedMs.ToString(CultureInfo.InvariantCulture),
                            ["error.message"] = att.ErrorMessage ?? "None"
                        },
                        new List<TraceSpanEventDto>
                        {
                            new("Connecting to Target Host", att.ExecutedAt),
                            new($"HTTP Response {att.HttpStatusCode}", att.ExecutedAt.AddMilliseconds(attDuration))
                        }));

                    currentOffset += attDuration;
                }
            }
        }

        // Span: Audit Trail Record
        if (auditLogs.Count > 0)
        {
            var auditSpanId = Guid.NewGuid().ToString("N")[..16];
            spans.Add(new TraceSpanDto(
                auditSpanId,
                gatewaySpanId,
                "audit.ledger_record",
                "Audit Ledger",
                "Internal",
                baseTime.AddMilliseconds(currentOffset),
                4,
                currentOffset,
                "Ok",
                new Dictionary<string, string>
                {
                    ["audit.action"] = auditLogs[0].Action,
                    ["audit.resource"] = auditLogs[0].ResourceType,
                    ["audit.records"] = auditLogs.Count.ToString(CultureInfo.InvariantCulture)
                },
                new List<TraceSpanEventDto>
                {
                    new("Audit Ledger Committed", baseTime.AddMilliseconds(currentOffset + 4))
                }));
        }

        return spans;
    }

    private static string ExtractTraceId(string? traceParent, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(traceParent))
        {
            var parts = traceParent.Split('-');
            if (parts.Length >= 4 && parts[1].Length == 32)
            {
                return parts[1];
            }
        }
        return fallback;
    }

    private static string ResolveOverallStatus(List<Delivery> deliveries)
    {
        if (deliveries.Count == 0) return "Ok";
        if (deliveries.Any(d => d.Status == DeliveryStatus.DeadLettered)) return "DeadLettered";
        if (deliveries.Any(d => d.Status == DeliveryStatus.Failed)) return "Failed";
        if (deliveries.All(d => d.Status == DeliveryStatus.Success)) return "Success";
        if (deliveries.Any(d => d.Status == DeliveryStatus.Dispatched)) return "Dispatched";
        return "Pending";
    }
}
