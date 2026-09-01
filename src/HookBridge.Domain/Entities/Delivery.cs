using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;

namespace HookBridge.Domain.Entities;

public sealed class Delivery : AggregateRoot<Guid>, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid EndpointId { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public DeliveryStatus Status { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public int AttemptCount { get; private set; }
    public string? TraceParent { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public Guid? OriginalDeliveryId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public ICollection<Attempt> Attempts { get; private set; } = new List<Attempt>();

    private Delivery() { }

    public static Result<Delivery> Create(
        Guid tenantId,
        Guid eventId,
        Guid endpointId,
        Guid subscriptionId,
        string eventType,
        string correlationId,
        string? traceParent,
        DateTimeOffset now,
        Guid? originalDeliveryId = null)
    {
        if (tenantId == Guid.Empty || eventId == Guid.Empty || endpointId == Guid.Empty)
        {
            return Result.Failure<Delivery>(DomainError.Validation("Delivery.InvalidParameters", "TenantId, EventId and EndpointId are mandatory."));
        }

        return Result.Success(new Delivery
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventId = eventId,
            EndpointId = endpointId,
            SubscriptionId = subscriptionId,
            EventType = eventType.Trim(),
            Status = DeliveryStatus.Pending,
            ScheduledAt = now,
            AttemptCount = 0,
            TraceParent = traceParent?.Trim(),
            CorrelationId = correlationId.Trim(),
            OriginalDeliveryId = originalDeliveryId,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    public void MarkDispatched(DateTimeOffset now)
    {
        Status = DeliveryStatus.Dispatched;
        AttemptCount++;
        UpdatedAt = now;
    }

    public void MarkSuccess(DateTimeOffset now)
    {
        Status = DeliveryStatus.Success;
        DeliveredAt = now;
        UpdatedAt = now;
    }

    public void MarkFailed(DateTimeOffset now)
    {
        Status = DeliveryStatus.Failed;
        UpdatedAt = now;
    }

    public void MarkDeadLettered(DateTimeOffset now)
    {
        Status = DeliveryStatus.DeadLettered;
        UpdatedAt = now;
    }
}
