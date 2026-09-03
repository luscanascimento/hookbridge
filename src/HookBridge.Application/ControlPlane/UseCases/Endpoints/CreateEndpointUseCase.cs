using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Endpoints;

public sealed class CreateEndpointUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateEndpointCommand> _validator;
    private readonly ISsrfGuard _ssrfGuard;
    private readonly IApiKeyGenerator _keyGenerator;
    private readonly ISecretEncryptor _secretEncryptor;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateEndpointUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IValidator<CreateEndpointCommand> validator,
        ISsrfGuard ssrfGuard,
        IApiKeyGenerator keyGenerator,
        ISecretEncryptor secretEncryptor,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _validator = validator;
        _ssrfGuard = ssrfGuard;
        _keyGenerator = keyGenerator;
        _secretEncryptor = secretEncryptor;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<EndpointCreatedResponse>> ExecuteAsync(CreateEndpointCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<EndpointCreatedResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<EndpointCreatedResponse>(DomainError.Validation(validation.Errors[0].PropertyName, validation.Errors[0].ErrorMessage));
        }

        // 1. Verify Parent Application exists within this tenant boundary
        var appExists = await _dbContext.Applications
            .AnyAsync(a => a.Id == command.ApplicationId && a.TenantId == tenantId, cancellationToken);

        if (!appExists)
        {
            return Result.Failure<EndpointCreatedResponse>(DomainError.NotFound(
                "Application.NotFound",
                $"Application with ID '{command.ApplicationId}' does not exist for this tenant."));
        }

        // 2. Validate Target URL with SSRF Guard
        var ssrfResult = await _ssrfGuard.ValidateUrlAsync(command.TargetUrl, cancellationToken);
        if (ssrfResult.IsFailure)
        {
            return Result.Failure<EndpointCreatedResponse>(ssrfResult.Error);
        }

        var now = _dateTimeProvider.UtcNow;

        // 3. Create Endpoint
        var endpointResult = Endpoint.Create(
            tenantId,
            command.ApplicationId,
            command.TargetUrl,
            command.Description,
            now,
            command.RateLimitPerMinute,
            command.TimeoutSeconds);

        if (endpointResult.IsFailure)
        {
            return Result.Failure<EndpointCreatedResponse>(endpointResult.Error);
        }

        var endpoint = endpointResult.Value;

        // 4. Provision Initial Webhook Secret
        var (plainSecret, secretPrefix, secretHash) = _keyGenerator.GenerateWebhookSecret();
        var encryptedSecret = _secretEncryptor.Encrypt(plainSecret);

        var secretResult = WebhookSecret.Create(
            tenantId,
            endpoint.Id,
            secretPrefix,
            secretHash,
            encryptedSecret,
            version: 1,
            now);

        if (secretResult.IsFailure)
        {
            return Result.Failure<EndpointCreatedResponse>(secretResult.Error);
        }

        var initialSecret = secretResult.Value;

        // 5. Provision Subscriptions if provided
        var subscribedEventPatterns = new List<string>();
        if (command.SubscribedEvents != null && command.SubscribedEvents.Count > 0)
        {
            foreach (var pattern in command.SubscribedEvents.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    var subResult = Subscription.Create(tenantId, endpoint.Id, pattern, now);
                    if (subResult.IsSuccess)
                    {
                        _dbContext.Subscriptions.Add(subResult.Value);
                        subscribedEventPatterns.Add(subResult.Value.EventTypePattern);
                    }
                }
            }
        }

        // 6. Audit Trail
        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "Endpoint.Created",
            "Endpoint",
            endpoint.Id.ToString(),
            JsonSerializer.Serialize(new { endpoint.TargetUrl, endpoint.ApplicationId, endpoint.RateLimitPerMinute, endpoint.TimeoutSeconds }),
            null,
            null,
            now).Value;

        _dbContext.Endpoints.Add(endpoint);
        _dbContext.WebhookSecrets.Add(initialSecret);
        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new EndpointCreatedResponse(
            endpoint.Id,
            endpoint.TenantId,
            endpoint.ApplicationId,
            endpoint.TargetUrl,
            endpoint.Description,
            endpoint.Status,
            endpoint.RateLimitPerMinute,
            endpoint.TimeoutSeconds,
            plainSecret,
            initialSecret.KeyPrefix,
            initialSecret.Version,
            subscribedEventPatterns,
            endpoint.CreatedAt));
    }
}
