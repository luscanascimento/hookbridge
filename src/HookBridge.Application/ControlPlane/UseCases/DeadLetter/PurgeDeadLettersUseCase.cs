using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;

namespace HookBridge.Application.ControlPlane.UseCases.DeadLetter;

public sealed class PurgeDeadLettersUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly IEventFlowClient _eventFlowClient;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PurgeDeadLettersUseCase(
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

    public async Task<Result<DeadLetterPurgeResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<DeadLetterPurgeResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var purgeResult = await _eventFlowClient.PurgeDlqAsync(cancellationToken);
        if (purgeResult.IsFailure)
        {
            return Result.Failure<DeadLetterPurgeResponse>(purgeResult.Error);
        }

        var now = _dateTimeProvider.UtcNow;
        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "DLQ.Purged",
            "DeadLetterQueue",
            "DLQ",
            JsonSerializer.Serialize(new { PurgedCount = purgeResult.Value }),
            null,
            null,
            now).Value;

        _dbContext.AuditEntries.Add(audit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new DeadLetterPurgeResponse(purgeResult.Value, "Purged"));
    }
}
