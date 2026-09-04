using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Diagnostics;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Deliveries;

public sealed class BulkReplayDeliveriesUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IEventFlowClient _eventFlowClient;
    private readonly IValidator<BulkReplayDeliveriesCommand> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BulkReplayDeliveriesUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IEventFlowClient eventFlowClient,
        IValidator<BulkReplayDeliveriesCommand> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _eventFlowClient = eventFlowClient;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<BulkReplayDeliveriesResponse>> ExecuteAsync(
        BulkReplayDeliveriesCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<BulkReplayDeliveriesResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<BulkReplayDeliveriesResponse>(DomainError.Validation(validation.Errors[0].PropertyName, validation.Errors[0].ErrorMessage));
        }

        using var activity = HookBridgeDiagnostics.ActivitySource.StartActivity("HookBridge.BulkReplayDeliveries");
        activity?.SetTag("tenant.id", tenantId.ToString());

        var maxCount = Math.Clamp(command.MaxCount ?? 50, 1, 500);

        List<Delivery> sourceDeliveries;

        if (command.DeliveryIds != null && command.DeliveryIds.Count > 0)
        {
            sourceDeliveries = await _dbContext.Deliveries
                .Where(d => d.TenantId == tenantId && command.DeliveryIds.Contains(d.Id))
                .Take(maxCount)
                .ToListAsync(cancellationToken);
        }
        else
        {
            var query = _dbContext.Deliveries
                .Where(d => d.TenantId == tenantId);

            if (command.EndpointId.HasValue && command.EndpointId.Value != Guid.Empty)
            {
                query = query.Where(d => d.EndpointId == command.EndpointId.Value);
            }

            if (command.Status.HasValue)
            {
                query = query.Where(d => d.Status == command.Status.Value);
            }
            else
            {
                // Default bulk replay targets failed and deadlettered deliveries
                query = query.Where(d => d.Status == DeliveryStatus.Failed || d.Status == DeliveryStatus.DeadLettered);
            }

            if (!string.IsNullOrWhiteSpace(command.EventType))
            {
                var eventType = command.EventType.Trim();
                query = query.Where(d => d.EventType == eventType);
            }

            if (command.FromDate.HasValue)
            {
                query = query.Where(d => d.CreatedAt >= command.FromDate.Value);
            }

            if (command.ToDate.HasValue)
            {
                query = query.Where(d => d.CreatedAt <= command.ToDate.Value);
            }

            sourceDeliveries = await query
                .OrderByDescending(d => d.CreatedAt)
                .Take(maxCount)
                .ToListAsync(cancellationToken);
        }

        if (sourceDeliveries.Count == 0)
        {
            return Result.Success(new BulkReplayDeliveriesResponse(0, Array.Empty<ReplayDeliveryResponse>()));
        }

        var endpointIds = sourceDeliveries.Select(d => d.EndpointId).Distinct().ToList();
        var endpoints = await _dbContext.Endpoints
            .Where(e => endpointIds.Contains(e.Id) && e.TenantId == tenantId)
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var sourceDeliveryIds = sourceDeliveries.Select(d => d.Id).ToList();
        var attempts = await _dbContext.Attempts
            .Where(a => sourceDeliveryIds.Contains(a.DeliveryId) && a.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var latestAttemptByDelivery = attempts
            .GroupBy(a => a.DeliveryId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AttemptNumber).FirstOrDefault());

        var now = _dateTimeProvider.UtcNow;
        var replayedResponses = new List<ReplayDeliveryResponse>();

        foreach (var orig in sourceDeliveries)
        {
            if (!endpoints.TryGetValue(orig.EndpointId, out var endpoint) || endpoint.Status == EndpointStatus.Disabled)
            {
                continue;
            }

            JsonElement payload;
            if (latestAttemptByDelivery.TryGetValue(orig.Id, out var latestAttempt) &&
                latestAttempt != null &&
                !string.IsNullOrWhiteSpace(latestAttempt.RequestBody))
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

            var spanId = Guid.NewGuid().ToString("N")[..16];
            var traceParent = $"00-{Guid.NewGuid():N}-{spanId}-01";
            var correlationId = !string.IsNullOrWhiteSpace(orig.CorrelationId)
                ? orig.CorrelationId
                : $"corr_{Guid.NewGuid():N}";

            var newDeliveryResult = Delivery.Create(
                tenantId,
                orig.EventId,
                endpoint.Id,
                orig.SubscriptionId,
                orig.EventType,
                correlationId,
                traceParent,
                now,
                orig.Id);

            if (newDeliveryResult.IsFailure)
            {
                continue;
            }

            var newDelivery = newDeliveryResult.Value;
            _dbContext.Deliveries.Add(newDelivery);

            var metadata = new Dictionary<string, string>
            {
                ["OriginalDeliveryId"] = orig.Id.ToString(),
                ["ReplayDeliveryId"] = newDelivery.Id.ToString(),
                ["ReplayedAt"] = now.ToString("O"),
                ["ReplayedBy"] = _currentUser.UserId?.ToString() ?? "system",
                ["BatchReplay"] = "true"
            };

            var ingestRequest = new EventFlowIngestRequest(
                orig.EventId,
                orig.EventType,
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
                continue;
            }

            replayedResponses.Add(new ReplayDeliveryResponse(
                newDelivery.Id,
                orig.Id,
                endpoint.Id,
                newDelivery.EventType,
                newDelivery.Status,
                newDelivery.ScheduledAt,
                newDelivery.TraceParent ?? string.Empty,
                newDelivery.CorrelationId));
        }

        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "Delivery.BulkReplayed",
            "Delivery",
            tenantId.ToString(),
            JsonSerializer.Serialize(new
            {
                ReplayedCount = replayedResponses.Count,
                RequestedMax = maxCount,
                FilterEndpointId = command.EndpointId,
                FilterStatus = command.Status?.ToString(),
                FilterEventType = command.EventType,
                ExplicitIdsCount = command.DeliveryIds?.Count ?? 0
            }),
            null,
            activity?.Id,
            now).Value;

        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        HookBridgeDiagnostics.ReplaysTriggered.Add(replayedResponses.Count);
        HookBridgeDiagnostics.DeliveriesDispatched.Add(replayedResponses.Count);

        return Result.Success(new BulkReplayDeliveriesResponse(replayedResponses.Count, replayedResponses));
    }
}
