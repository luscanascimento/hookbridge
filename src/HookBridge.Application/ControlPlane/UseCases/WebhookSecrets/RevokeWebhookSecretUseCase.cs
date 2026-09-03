using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.WebhookSecrets;

public sealed class RevokeWebhookSecretUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RevokeWebhookSecretUseCase(
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

    public async Task<Result> ExecuteAsync(Guid endpointId, Guid secretId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var secret = await _dbContext.WebhookSecrets
            .FirstOrDefaultAsync(s => s.Id == secretId && s.EndpointId == endpointId && s.TenantId == tenantId, cancellationToken);

        if (secret is null)
        {
            return Result.Failure(DomainError.NotFound(
                "WebhookSecret.NotFound",
                $"Webhook secret with ID '{secretId}' was not found on endpoint '{endpointId}'."));
        }

        if (secret.Status == SecretStatus.Revoked)
        {
            return Result.Failure(DomainError.Conflict(
                "WebhookSecret.AlreadyRevoked",
                "This webhook secret is already revoked."));
        }

        var now = _dateTimeProvider.UtcNow;
        secret.Revoke(now);

        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "WebhookSecret.Revoked",
            "WebhookSecret",
            secret.Id.ToString(),
            JsonSerializer.Serialize(new { EndpointId = endpointId, Version = secret.Version, Prefix = secret.KeyPrefix }),
            null,
            null,
            now).Value;

        _dbContext.AuditEntries.Add(audit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
