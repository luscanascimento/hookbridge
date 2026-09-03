using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.WebhookSigning;

public sealed class GenerateEndpointSignatureUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IWebhookSigner _webhookSigner;
    private readonly ISecretEncryptor _secretEncryptor;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GenerateEndpointSignatureUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        IWebhookSigner webhookSigner,
        ISecretEncryptor secretEncryptor,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _webhookSigner = webhookSigner;
        _secretEncryptor = secretEncryptor;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<GenerateSignatureResponse>> ExecuteAsync(Guid endpointId, GenerateSignatureCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<GenerateSignatureResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var endpoint = await _dbContext.Endpoints
            .Include(e => e.Secrets)
            .FirstOrDefaultAsync(e => e.Id == endpointId && e.TenantId == tenantId, cancellationToken);

        if (endpoint is null)
        {
            return Result.Failure<GenerateSignatureResponse>(DomainError.NotFound(
                "Endpoint.NotFound",
                $"Endpoint with ID '{endpointId}' was not found."));
        }

        // Active and Rotating secrets participate in signature generation (dual-signing during rotation)
        var validSecrets = endpoint.Secrets
            .Where(s => s.Status == SecretStatus.Active || s.Status == SecretStatus.Rotating)
            .OrderByDescending(s => s.Status == SecretStatus.Active) // Active first
            .ThenByDescending(s => s.Version)
            .ToList();

        if (validSecrets.Count == 0)
        {
            return Result.Failure<GenerateSignatureResponse>(DomainError.Conflict(
                "Endpoint.NoActiveSecret",
                "This endpoint does not have any active or rotating webhook signing secrets."));
        }

        var decryptedSecrets = validSecrets.Select(s => _secretEncryptor.Decrypt(s.EncryptedSecret)).ToList();
        var timestamp = command.Timestamp ?? _dateTimeProvider.UtcNow;
        var header = _webhookSigner.GenerateSignatureHeader(command.Payload, decryptedSecrets, timestamp);

        var unixTimestamp = timestamp.ToUnixTimeSeconds();
        var signatures = decryptedSecrets.Select(k => _webhookSigner.ComputeHmacSha256(command.Payload, k, unixTimestamp)).ToList();

        return Result.Success(new GenerateSignatureResponse(
            header,
            unixTimestamp,
            signatures,
            decryptedSecrets.Count));
    }
}
