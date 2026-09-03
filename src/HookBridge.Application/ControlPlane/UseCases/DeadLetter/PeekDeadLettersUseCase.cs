using HookBridge.Application.Abstractions;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;

namespace HookBridge.Application.ControlPlane.UseCases.DeadLetter;

public sealed class PeekDeadLettersUseCase
{
    private readonly IEventFlowClient _eventFlowClient;
    private readonly ITenantContext _tenantContext;

    public PeekDeadLettersUseCase(IEventFlowClient eventFlowClient, ITenantContext tenantContext)
    {
        _eventFlowClient = eventFlowClient;
        _tenantContext = tenantContext;
    }

    public async Task<Result<DeadLetterListResponse>> ExecuteAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<DeadLetterListResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var peekResult = await _eventFlowClient.PeekDlqAsync(count, cancellationToken);
        if (peekResult.IsFailure)
        {
            return Result.Failure<DeadLetterListResponse>(peekResult.Error);
        }

        var messages = peekResult.Value;
        return Result.Success(new DeadLetterListResponse(messages.Count, messages));
    }
}
