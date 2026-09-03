using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.ApiKeys;

public sealed class CreateApiKeyUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateApiKeyCommand> _validator;
    private readonly IApiKeyGenerator _keyGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateApiKeyUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IValidator<CreateApiKeyCommand> validator,
        IApiKeyGenerator keyGenerator,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _validator = validator;
        _keyGenerator = keyGenerator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ApiKeyCreatedResponse>> ExecuteAsync(CreateApiKeyCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<ApiKeyCreatedResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ApiKeyCreatedResponse>(DomainError.Validation(validation.Errors[0].PropertyName, validation.Errors[0].ErrorMessage));
        }

        var trimmedName = command.Name.Trim();
        var now = _dateTimeProvider.UtcNow;

        var (plainKey, keyPrefix, keyHash) = _keyGenerator.GenerateApiKey(command.Environment);

        var apiKeyResult = ApiKey.Create(
            tenantId,
            trimmedName,
            keyPrefix,
            keyHash,
            command.Scopes,
            now,
            command.ExpiresAt);

        if (apiKeyResult.IsFailure)
        {
            return Result.Failure<ApiKeyCreatedResponse>(apiKeyResult.Error);
        }

        var apiKey = apiKeyResult.Value;

        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "ApiKey.Created",
            "ApiKey",
            apiKey.Id.ToString(),
            JsonSerializer.Serialize(new { apiKey.Name, apiKey.KeyPrefix, Scopes = apiKey.Scopes.ToString(), apiKey.ExpiresAt }),
            null,
            null,
            now).Value;

        _dbContext.ApiKeys.Add(apiKey);
        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ApiKeyCreatedResponse(
            apiKey.Id,
            apiKey.TenantId,
            apiKey.Name,
            plainKey,
            apiKey.KeyPrefix,
            apiKey.Scopes,
            apiKey.ExpiresAt,
            apiKey.CreatedAt));
    }
}
