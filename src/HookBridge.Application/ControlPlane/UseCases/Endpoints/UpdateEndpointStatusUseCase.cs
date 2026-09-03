using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Endpoints;

public sealed class UpdateEndpointStatusUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<UpdateEndpointStatusCommand> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateEndpointStatusUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IValidator<UpdateEndpointStatusCommand> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<EndpointResponse>> ExecuteAsync(Guid endpointId, UpdateEndpointStatusCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<EndpointResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<EndpointResponse>(DomainError.Validation(validation.Errors[0].PropertyName, validation.Errors[0].ErrorMessage));
        }

        var endpoint = await _dbContext.Endpoints
            .Include(e => e.Secrets)
            .Include(e => e.Subscriptions)
            .FirstOrDefaultAsync(e => e.Id == endpointId && e.TenantId == tenantId, cancellationToken);

        if (endpoint is null)
        {
            return Result.Failure<EndpointResponse>(DomainError.NotFound(
                "Endpoint.NotFound",
                $"Endpoint with ID '{endpointId}' was not found."));
        }

        var now = _dateTimeProvider.UtcNow;
        endpoint.SetStatus(command.Status, command.Reason, now);

        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "Endpoint.StatusChanged",
            "Endpoint",
            endpoint.Id.ToString(),
            JsonSerializer.Serialize(new { Status = command.Status.ToString(), command.Reason }),
            null,
            null,
            now).Value;

        _dbContext.AuditEntries.Add(audit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var activeSecret = endpoint.Secrets.FirstOrDefault(s => s.Status == SecretStatus.Active)
            ?? endpoint.Secrets.OrderByDescending(s => s.Version).FirstOrDefault();

        var events = endpoint.Subscriptions
            .Where(s => s.IsActive)
            .Select(s => s.EventTypePattern)
            .ToList();

        return Result.Success(new EndpointResponse(
            endpoint.Id,
            endpoint.TenantId,
            endpoint.ApplicationId,
            endpoint.TargetUrl,
            endpoint.Description,
            endpoint.Status,
            endpoint.DisabledReason,
            endpoint.RateLimitPerMinute,
            endpoint.TimeoutSeconds,
            activeSecret?.KeyPrefix,
            activeSecret?.Version ?? 1,
            events,
            endpoint.CreatedAt,
            endpoint.UpdatedAt));
    }
}
