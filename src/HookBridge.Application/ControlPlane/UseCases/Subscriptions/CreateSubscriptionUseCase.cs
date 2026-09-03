using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Subscriptions;

public sealed class CreateSubscriptionUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateSubscriptionCommand> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateSubscriptionUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IValidator<CreateSubscriptionCommand> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<SubscriptionResponse>> ExecuteAsync(Guid endpointId, CreateSubscriptionCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<SubscriptionResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SubscriptionResponse>(DomainError.Validation(validation.Errors[0].PropertyName, validation.Errors[0].ErrorMessage));
        }

        var endpointExists = await _dbContext.Endpoints
            .AnyAsync(e => e.Id == endpointId && e.TenantId == tenantId, cancellationToken);

        if (!endpointExists)
        {
            return Result.Failure<SubscriptionResponse>(DomainError.NotFound(
                "Endpoint.NotFound",
                $"Endpoint with ID '{endpointId}' does not exist for this tenant."));
        }

        var pattern = command.EventTypePattern.Trim().ToLowerInvariant();
        var alreadySubscribed = await _dbContext.Subscriptions
            .AnyAsync(s => s.EndpointId == endpointId && s.TenantId == tenantId && s.EventTypePattern == pattern, cancellationToken);

        if (alreadySubscribed)
        {
            return Result.Failure<SubscriptionResponse>(DomainError.Conflict(
                "Subscription.AlreadyExists",
                $"A subscription for event pattern '{pattern}' already exists on this endpoint."));
        }

        var now = _dateTimeProvider.UtcNow;
        var subResult = Subscription.Create(tenantId, endpointId, pattern, now);
        if (subResult.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(subResult.Error);
        }

        var subscription = subResult.Value;

        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "Subscription.Created",
            "Subscription",
            subscription.Id.ToString(),
            JsonSerializer.Serialize(new { subscription.EndpointId, subscription.EventTypePattern }),
            null,
            null,
            now).Value;

        _dbContext.Subscriptions.Add(subscription);
        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new SubscriptionResponse(
            subscription.Id,
            subscription.TenantId,
            subscription.EndpointId,
            subscription.EventTypePattern,
            subscription.IsActive,
            subscription.CreatedAt));
    }
}
