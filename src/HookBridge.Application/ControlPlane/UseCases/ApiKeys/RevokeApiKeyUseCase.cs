using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HookBridge.Application.ControlPlane.UseCases.ApiKeys;

public sealed partial class RevokeApiKeyUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<RevokeApiKeyUseCase> _logger;

    public RevokeApiKeyUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ILogger<RevokeApiKeyUseCase>? logger = null)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger ?? NullLogger<RevokeApiKeyUseCase>.Instance;
    }

    [LoggerMessage(EventId = 4041, Level = LogLevel.Information, Message = "API Key revoked: ApiKeyId={ApiKeyId}, TenantId={TenantId}")]
    private static partial void LogApiKeyRevoked(ILogger logger, Guid apiKeyId, Guid tenantId);

    public async Task<Result> ExecuteAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var apiKey = await _dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == apiKeyId && k.TenantId == tenantId, cancellationToken);

        if (apiKey is null)
        {
            return Result.Failure(DomainError.NotFound(
                "ApiKey.NotFound",
                $"API Key with ID '{apiKeyId}' was not found."));
        }

        if (apiKey.RevokedAt.HasValue)
        {
            return Result.Failure(DomainError.Conflict(
                "ApiKey.AlreadyRevoked",
                "This API Key is already revoked."));
        }

        var now = _dateTimeProvider.UtcNow;
        apiKey.Revoke(now);

        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "ApiKey.Revoked",
            "ApiKey",
            apiKey.Id.ToString(),
            JsonSerializer.Serialize(new { apiKey.Name, apiKey.KeyPrefix }),
            _currentUser.IpAddress,
            null,
            now).Value;

        _dbContext.AuditEntries.Add(audit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        LogApiKeyRevoked(_logger, apiKey.Id, tenantId);

        return Result.Success();
    }
}
