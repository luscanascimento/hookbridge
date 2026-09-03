using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.WebhookSecrets;

public sealed class RotateWebhookSecretUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IApiKeyGenerator _keyGenerator;
    private readonly ISecretEncryptor _secretEncryptor;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RotateWebhookSecretUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IApiKeyGenerator keyGenerator,
        ISecretEncryptor secretEncryptor,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _keyGenerator = keyGenerator;
        _secretEncryptor = secretEncryptor;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<RotateSecretResponse>> ExecuteAsync(Guid endpointId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<RotateSecretResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var endpoint = await _dbContext.Endpoints
            .Include(e => e.Secrets)
            .FirstOrDefaultAsync(e => e.Id == endpointId && e.TenantId == tenantId, cancellationToken);

        if (endpoint is null)
        {
            return Result.Failure<RotateSecretResponse>(DomainError.NotFound(
                "Endpoint.NotFound",
                $"Endpoint with ID '{endpointId}' was not found."));
        }

        var now = _dateTimeProvider.UtcNow;

        // 1. Transition any current Active secret to Rotating status
        var activeSecrets = endpoint.Secrets.Where(s => s.Status == SecretStatus.Active).ToList();
        foreach (var activeSec in activeSecrets)
        {
            activeSec.MarkRotating(now);
        }

        // 2. Determine new version
        var maxVersion = endpoint.Secrets.Count > 0 ? endpoint.Secrets.Max(s => s.Version) : 0;
        var nextVersion = maxVersion + 1;

        // 3. Generate new Webhook Secret and Encrypt
        var (plainSecret, secretPrefix, secretHash) = _keyGenerator.GenerateWebhookSecret();
        var encryptedSecret = _secretEncryptor.Encrypt(plainSecret);

        var newSecretResult = WebhookSecret.Create(
            tenantId,
            endpointId,
            secretPrefix,
            secretHash,
            encryptedSecret,
            nextVersion,
            now);

        if (newSecretResult.IsFailure)
        {
            return Result.Failure<RotateSecretResponse>(newSecretResult.Error);
        }

        var newSecret = newSecretResult.Value;

        // 4. Audit entry
        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "WebhookSecret.Rotated",
            "WebhookSecret",
            newSecret.Id.ToString(),
            JsonSerializer.Serialize(new { EndpointId = endpointId, Version = nextVersion, Prefix = secretPrefix }),
            null,
            null,
            now).Value;

        _dbContext.WebhookSecrets.Add(newSecret);
        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new RotateSecretResponse(
            newSecret.Id,
            endpointId,
            plainSecret,
            newSecret.KeyPrefix,
            newSecret.Version,
            newSecret.Status,
            newSecret.CreatedAt));
    }
}
