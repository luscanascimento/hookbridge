using HookBridge.Application.Abstractions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Diagnostics;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HookBridge.Application.ControlPlane.UseCases.Deliveries;

public sealed partial class RecordDeliveryAttemptUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDeliveryRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<RecordDeliveryAttemptUseCase> _logger;

    public RecordDeliveryAttemptUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider,
        IDeliveryRealtimeNotifier? realtimeNotifier = null,
        ILogger<RecordDeliveryAttemptUseCase>? logger = null)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
        _realtimeNotifier = realtimeNotifier ?? NullDeliveryRealtimeNotifier.Instance;
        _logger = logger ?? NullLogger<RecordDeliveryAttemptUseCase>.Instance;
    }

    [LoggerMessage(EventId = 3101, Level = LogLevel.Information, Message = "Delivery attempt recorded: DeliveryId={DeliveryId}, AttemptNumber={AttemptNumber}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}, Status={Status}, TenantId={TenantId}")]
    private static partial void LogAttemptRecorded(ILogger logger, Guid deliveryId, int attemptNumber, int? statusCode, long elapsedMs, DeliveryStatus status, Guid tenantId);

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

        var sanitizedReqHeaders = SensitiveDataSanitizer.SanitizeHeadersJson(command.RequestHeadersJson);
        var sanitizedResHeaders = !string.IsNullOrWhiteSpace(command.ResponseHeadersJson)
            ? SensitiveDataSanitizer.SanitizeHeadersJson(command.ResponseHeadersJson)
            : null;

        var attemptResult = Attempt.Create(
            delivery.Id,
            tenantId,
            attemptNumber,
            command.HttpStatusCode,
            sanitizedReqHeaders,
            command.RequestBody ?? "{}",
            sanitizedResHeaders,
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

        // Emit realtime SignalR delivery attempt notification
        await _realtimeNotifier.NotifyDeliveryAttemptRecordedAsync(delivery, attempt, cancellationToken);

        LogAttemptRecorded(_logger, delivery.Id, attempt.AttemptNumber, attempt.HttpStatusCode, attempt.ElapsedMs, delivery.Status, tenantId);

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
