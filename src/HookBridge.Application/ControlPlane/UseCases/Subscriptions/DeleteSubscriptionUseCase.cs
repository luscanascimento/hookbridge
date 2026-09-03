using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Subscriptions;

public sealed class DeleteSubscriptionUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeleteSubscriptionUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> ExecuteAsync(Guid endpointId, Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.EndpointId == endpointId && s.TenantId == tenantId, cancellationToken);

        if (subscription is null)
        {
            return Result.Failure(DomainError.NotFound(
                "Subscription.NotFound",
                $"Subscription with ID '{subscriptionId}' was not found for endpoint '{endpointId}'."));
        }

        var now = _dateTimeProvider.UtcNow;
        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "Subscription.Deleted",
            "Subscription",
            subscription.Id.ToString(),
            JsonSerializer.Serialize(new { subscription.EndpointId, subscription.EventTypePattern }),
            null,
            null,
            now).Value;

        _dbContext.Subscriptions.Remove(subscription);
        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
