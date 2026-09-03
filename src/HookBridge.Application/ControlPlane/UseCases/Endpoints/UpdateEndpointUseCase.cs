using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Endpoints;

public sealed class UpdateEndpointUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<UpdateEndpointCommand> _validator;
    private readonly ISsrfGuard _ssrfGuard;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateEndpointUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IValidator<UpdateEndpointCommand> validator,
        ISsrfGuard ssrfGuard,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _validator = validator;
        _ssrfGuard = ssrfGuard;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<EndpointResponse>> ExecuteAsync(Guid endpointId, UpdateEndpointCommand command, CancellationToken cancellationToken = default)
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

        // Validate SSRF Guard on TargetUrl if modified
        if (!string.Equals(endpoint.TargetUrl, command.TargetUrl.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var ssrfResult = await _ssrfGuard.ValidateUrlAsync(command.TargetUrl, cancellationToken);
            if (ssrfResult.IsFailure)
            {
                return Result.Failure<EndpointResponse>(ssrfResult.Error);
            }
        }

        var now = _dateTimeProvider.UtcNow;
        endpoint.UpdateDetails(command.TargetUrl, command.Description, command.RateLimitPerMinute, command.TimeoutSeconds, now);

        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "Endpoint.Updated",
            "Endpoint",
            endpoint.Id.ToString(),
            JsonSerializer.Serialize(new { endpoint.TargetUrl, endpoint.Description, endpoint.RateLimitPerMinute, endpoint.TimeoutSeconds }),
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
