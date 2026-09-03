using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Diagnostics;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Deliveries;

public sealed class RecordDeliveryAttemptUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RecordDeliveryAttemptUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<AttemptResponse>> ExecuteAsync(Guid deliveryId, RecordDeliveryAttemptCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<AttemptResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var delivery = await _dbContext.Deliveries
            .FirstOrDefaultAsync(d => d.Id == deliveryId && d.TenantId == tenantId, cancellationToken);

        if (delivery is null)
        {
            return Result.Failure<AttemptResponse>(DomainError.NotFound(
                "Delivery.NotFound",
                $"Delivery with ID '{deliveryId}' was not found."));
        }

        var now = _dateTimeProvider.UtcNow;
        var attemptNumber = delivery.AttemptCount + 1;

        var attemptResult = Attempt.Create(
            delivery.Id,
            tenantId,
            attemptNumber,
            command.HttpStatusCode,
            command.RequestHeadersJson ?? "{}",
            command.RequestBody ?? "{}",
            command.ResponseHeadersJson,
            command.ResponseBody,
            command.ElapsedMs,
            command.ErrorMessage,
            now);

        if (attemptResult.IsFailure)
        {
            return Result.Failure<AttemptResponse>(attemptResult.Error);
        }

        var attempt = attemptResult.Value;
        _dbContext.Attempts.Add(attempt);

        // Transition delivery status
        delivery.MarkDispatched(now);
        switch (command.FinalStatus)
        {
            case DeliveryStatus.Success:
                delivery.MarkSuccess(now);
                HookBridgeDiagnostics.DeliveriesSucceeded.Add(1);
                break;
            case DeliveryStatus.Failed:
                delivery.MarkFailed(now);
                HookBridgeDiagnostics.DeliveriesFailed.Add(1);
                break;
            case DeliveryStatus.DeadLettered:
                delivery.MarkDeadLettered(now);
                HookBridgeDiagnostics.DeliveriesFailed.Add(1);
                break;
        }

        HookBridgeDiagnostics.DeliveryLatency.Record(command.ElapsedMs);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AttemptResponse(
            attempt.Id,
            attempt.DeliveryId,
            attempt.AttemptNumber,
            attempt.HttpStatusCode,
            attempt.RequestHeadersJson,
            attempt.RequestBody,
            attempt.ResponseHeadersJson,
            attempt.ResponseBody,
            attempt.ElapsedMs,
            attempt.ErrorMessage,
            attempt.ExecutedAt));
    }
}
