using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;

namespace HookBridge.Application.ControlPlane.UseCases.DeadLetter;

public sealed class ReplayDeadLettersUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly IEventFlowClient _eventFlowClient;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReplayDeadLettersUseCase(
        IHookBridgeDbContext dbContext,
        IEventFlowClient eventFlowClient,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _eventFlowClient = eventFlowClient;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<DeadLetterReplayResponse>> ExecuteAsync(int maxCount = 50, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<DeadLetterReplayResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var replayResult = await _eventFlowClient.ReplayDlqAsync(maxCount, cancellationToken);
        if (replayResult.IsFailure)
        {
            return Result.Failure<DeadLetterReplayResponse>(replayResult.Error);
        }

        var now = _dateTimeProvider.UtcNow;
        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "DLQ.Replayed",
            "DeadLetterQueue",
            "DLQ",
            JsonSerializer.Serialize(new { ReplayedCount = replayResult.Value, RequestedMax = maxCount }),
            null,
            null,
            now).Value;

        _dbContext.AuditEntries.Add(audit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new DeadLetterReplayResponse(replayResult.Value, "Replayed"));
    }
}
