using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.WebhookSigning;

public sealed class VerifyEndpointSignatureUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IWebhookSigner _webhookSigner;
    private readonly ISecretEncryptor _secretEncryptor;
    private readonly IDateTimeProvider _dateTimeProvider;

    public VerifyEndpointSignatureUseCase(
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

    public async Task<Result<VerifySignatureResponse>> ExecuteAsync(Guid endpointId, VerifySignatureCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<VerifySignatureResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var endpoint = await _dbContext.Endpoints
            .Include(e => e.Secrets)
            .FirstOrDefaultAsync(e => e.Id == endpointId && e.TenantId == tenantId, cancellationToken);

        if (endpoint is null)
        {
            return Result.Failure<VerifySignatureResponse>(DomainError.NotFound(
                "Endpoint.NotFound",
                $"Endpoint with ID '{endpointId}' was not found."));
        }

        // Active and Rotating secrets are valid verification candidates
        var validSecrets = endpoint.Secrets
            .Where(s => s.Status == SecretStatus.Active || s.Status == SecretStatus.Rotating)
            .ToList();

        if (validSecrets.Count == 0)
        {
            return Result.Failure<VerifySignatureResponse>(DomainError.Conflict(
                "Endpoint.NoActiveSecret",
                "This endpoint does not have any active or rotating signing secrets."));
        }

        var candidateSecrets = validSecrets.Select(s => _secretEncryptor.Decrypt(s.EncryptedSecret)).ToList();
        var tolerance = TimeSpan.FromSeconds(Math.Clamp(command.ToleranceSeconds, 1, 3600));
        var now = _dateTimeProvider.UtcNow;

        var verificationResult = _webhookSigner.VerifySignature(
            command.Payload,
            command.SignatureHeader,
            candidateSecrets,
            tolerance,
            now);

        if (verificationResult.IsSuccess)
        {
            return Result.Success(new VerifySignatureResponse(true, "Valid", null));
        }

        return Result.Success(new VerifySignatureResponse(false, "Invalid", verificationResult.Error.Message));
    }
}
