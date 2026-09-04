using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Diagnostics;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Deliveries;

public sealed class ReplayDeliveryUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IEventFlowClient _eventFlowClient;
    private readonly IValidator<ReplayDeliveryCommand> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDeliveryRealtimeNotifier _realtimeNotifier;

    public ReplayDeliveryUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IEventFlowClient eventFlowClient,
        IValidator<ReplayDeliveryCommand> validator,
        IDateTimeProvider dateTimeProvider,
        IDeliveryRealtimeNotifier? realtimeNotifier = null)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _eventFlowClient = eventFlowClient;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
        _realtimeNotifier = realtimeNotifier ?? NullDeliveryRealtimeNotifier.Instance;
    }

    public async Task<Result<ReplayDeliveryResponse>> ExecuteAsync(
        Guid deliveryId,
        ReplayDeliveryCommand? command = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<ReplayDeliveryResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        if (command is not null)
        {
            var validation = await _validator.ValidateAsync(command, cancellationToken);
            if (!validation.IsValid)
            {
                return Result.Failure<ReplayDeliveryResponse>(DomainError.Validation(validation.Errors[0].PropertyName, validation.Errors[0].ErrorMessage));
            }
        }

        using var activity = HookBridgeDiagnostics.ActivitySource.StartActivity("HookBridge.ReplayDelivery");
        activity?.SetTag("tenant.id", tenantId.ToString());
        activity?.SetTag("original_delivery.id", deliveryId.ToString());

        var originalDelivery = await _dbContext.Deliveries
            .FirstOrDefaultAsync(d => d.Id == deliveryId && d.TenantId == tenantId, cancellationToken);

        if (originalDelivery is null)
        {
            return Result.Failure<ReplayDeliveryResponse>(DomainError.NotFound(
                "Delivery.NotFound",
                $"Delivery with ID '{deliveryId}' was not found."));
        }

        var targetEndpointId = command?.OverrideEndpointId ?? originalDelivery.EndpointId;
        var endpoint = await _dbContext.Endpoints
            .FirstOrDefaultAsync(e => e.Id == targetEndpointId && e.TenantId == tenantId, cancellationToken);

        if (endpoint is null)
        {
            return Result.Failure<ReplayDeliveryResponse>(DomainError.NotFound(
                "Endpoint.NotFound",
                $"Target endpoint with ID '{targetEndpointId}' was not found."));
        }

        if (endpoint.Status == EndpointStatus.Disabled)
        {
            return Result.Failure<ReplayDeliveryResponse>(DomainError.Validation(
                "Endpoint.Disabled",
                $"Target endpoint '{endpoint.TargetUrl}' is disabled."));
        }

        // Retrieve latest attempt's request payload if available
        var latestAttempt = await _dbContext.Attempts
            .Where(a => a.DeliveryId == originalDelivery.Id && a.TenantId == tenantId)
            .OrderByDescending(a => a.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);

        JsonElement payload;
        if (latestAttempt != null && !string.IsNullOrWhiteSpace(latestAttempt.RequestBody))
        {
            try
            {
                using var doc = JsonDocument.Parse(latestAttempt.RequestBody);
                payload = doc.RootElement.Clone();
            }
            catch
            {
                using var doc = JsonDocument.Parse("{}");
                payload = doc.RootElement.Clone();
            }
        }
        else
        {
            using var doc = JsonDocument.Parse("{}");
            payload = doc.RootElement.Clone();
        }

        var now = _dateTimeProvider.UtcNow;
        var spanId = Guid.NewGuid().ToString("N")[..16];
        var traceParent = activity?.Id ?? $"00-{Guid.NewGuid():N}-{spanId}-01";
        var correlationId = !string.IsNullOrWhiteSpace(originalDelivery.CorrelationId)
            ? originalDelivery.CorrelationId
            : $"corr_{Guid.NewGuid():N}";

        var newDeliveryResult = Delivery.Create(
            tenantId,
            originalDelivery.EventId,
            endpoint.Id,
            originalDelivery.SubscriptionId,
            originalDelivery.EventType,
            correlationId,
            traceParent,
            now,
            originalDelivery.Id);

        if (newDeliveryResult.IsFailure)
        {
            return Result.Failure<ReplayDeliveryResponse>(newDeliveryResult.Error);
        }

        var newDelivery = newDeliveryResult.Value;
        _dbContext.Deliveries.Add(newDelivery);

        // Ingest into EventFlow Data Plane
        var metadata = new Dictionary<string, string>
        {
            ["OriginalDeliveryId"] = originalDelivery.Id.ToString(),
            ["ReplayDeliveryId"] = newDelivery.Id.ToString(),
            ["ReplayedAt"] = now.ToString("O"),
            ["ReplayedBy"] = _currentUser.UserId?.ToString() ?? "system"
        };

        var ingestRequest = new EventFlowIngestRequest(
            originalDelivery.EventId,
            originalDelivery.EventType,
            1,
            "hookbridge-replay-engine",
            now,
            correlationId,
            traceParent,
            tenantId.ToString(),
            $"replay_{newDelivery.Id:N}",
            payload,
            metadata);

        var ingestResult = await _eventFlowClient.IngestEventAsync(ingestRequest, cancellationToken);
        if (ingestResult.IsFailure)
        {
            return Result.Failure<ReplayDeliveryResponse>(ingestResult.Error);
        }

        // Audit entry
        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "Delivery.Replayed",
            "Delivery",
            newDelivery.Id.ToString(),
            JsonSerializer.Serialize(new
            {
                OriginalDeliveryId = originalDelivery.Id,
                NewDeliveryId = newDelivery.Id,
                EndpointId = endpoint.Id,
                EventType = originalDelivery.EventType,
                OverrideEndpoint = command?.OverrideEndpointId.HasValue == true
            }),
            null,
            traceParent,
            now).Value;

        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Emit realtime SignalR delivery replayed notification
        await _realtimeNotifier.NotifyDeliveryReplayedAsync(newDelivery, originalDelivery.Id, cancellationToken);

        HookBridgeDiagnostics.ReplaysTriggered.Add(1);
        HookBridgeDiagnostics.DeliveriesDispatched.Add(1);

        return Result.Success(new ReplayDeliveryResponse(
            newDelivery.Id,
            originalDelivery.Id,
            endpoint.Id,
            newDelivery.EventType,
            newDelivery.Status,
            newDelivery.ScheduledAt,
            newDelivery.TraceParent ?? string.Empty,
            newDelivery.CorrelationId));
    }
}
