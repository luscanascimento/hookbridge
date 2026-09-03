using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Common;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Diagnostics;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Publishing;

public sealed class PublishEventUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IEventFlowClient _eventFlowClient;
    private readonly IValidator<PublishEventCommand> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PublishEventUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IEventFlowClient eventFlowClient,
        IValidator<PublishEventCommand> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _eventFlowClient = eventFlowClient;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<PublishEventResponse>> ExecuteAsync(PublishEventCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<PublishEventResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PublishEventResponse>(DomainError.Validation(validation.Errors[0].PropertyName, validation.Errors[0].ErrorMessage));
        }

        using var activity = HookBridgeDiagnostics.ActivitySource.StartActivity("HookBridge.PublishEvent");
        activity?.SetTag("tenant.id", tenantId.ToString());
        activity?.SetTag("event.type", command.EventType);

        var now = _dateTimeProvider.UtcNow;
        var eventId = Guid.NewGuid();
        var correlationId = !string.IsNullOrWhiteSpace(command.CorrelationId) ? command.CorrelationId.Trim() : $"corr_{Guid.NewGuid():N}";
        var idempotencyKey = !string.IsNullOrWhiteSpace(command.IdempotencyKey) ? command.IdempotencyKey.Trim() : $"idemp_{Guid.NewGuid():N}";
        var spanId = Guid.NewGuid().ToString("N")[..16];
        var traceParent = activity?.Id ?? $"00-{Guid.NewGuid():N}-{spanId}-01";

        activity?.SetTag("correlation.id", correlationId);
        activity?.SetTag("idempotency.key", idempotencyKey);

        // 1. Find all active endpoints with matching subscriptions within this tenant
        var endpoints = await _dbContext.Endpoints
            .Include(e => e.Subscriptions)
            .Where(e => e.TenantId == tenantId && e.Status == EndpointStatus.Active)
            .ToListAsync(cancellationToken);

        var deliveriesCreated = new List<Delivery>();
        foreach (var endpoint in endpoints)
        {
            var matchingSub = endpoint.Subscriptions
                .FirstOrDefault(s => s.IsActive && SubscriptionPatternMatcher.Matches(s.EventTypePattern, command.EventType));

            if (matchingSub != null)
            {
                var deliveryResult = Delivery.Create(
                    tenantId,
                    eventId,
                    endpoint.Id,
                    matchingSub.Id,
                    command.EventType,
                    correlationId,
                    traceParent,
                    now);

                if (deliveryResult.IsSuccess)
                {
                    deliveriesCreated.Add(deliveryResult.Value);
                    _dbContext.Deliveries.Add(deliveryResult.Value);
                }
            }
        }

        // 2. Ingest Event into EventFlow Data Plane
        var ingestRequest = new EventFlowIngestRequest(
            eventId,
            command.EventType,
            command.Version ?? 1,
            "hookbridge-control-plane",
            now,
            correlationId,
            traceParent,
            tenantId.ToString(),
            idempotencyKey,
            command.Payload,
            command.Metadata);

        var ingestResult = await _eventFlowClient.IngestEventAsync(ingestRequest, cancellationToken);
        if (ingestResult.IsFailure)
        {
            return Result.Failure<PublishEventResponse>(ingestResult.Error);
        }

        // 3. Audit trail
        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "Event.Published",
            "Event",
            eventId.ToString(),
            JsonSerializer.Serialize(new { command.EventType, DeliveriesCount = deliveriesCreated.Count, correlationId, idempotencyKey }),
            null,
            traceParent,
            now).Value;

        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Record metrics
        HookBridgeDiagnostics.DeliveriesDispatched.Add(deliveriesCreated.Count);

        return Result.Success(new PublishEventResponse(
            eventId,
            "Accepted",
            command.EventType,
            deliveriesCreated.Count,
            now,
            traceParent,
            correlationId));
    }
}
