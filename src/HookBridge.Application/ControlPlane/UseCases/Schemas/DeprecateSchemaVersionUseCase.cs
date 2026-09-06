using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Schemas;

public sealed class DeprecateSchemaVersionUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeprecateSchemaVersionUseCase(
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

    public async Task<Result<EventSchemaVersionResponse>> ExecuteAsync(
        Guid schemaId,
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<EventSchemaVersionResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var version = await _dbContext.EventSchemaVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.EventSchemaId == schemaId && v.TenantId == tenantId, cancellationToken);

        if (version == null)
        {
            return Result.Failure<EventSchemaVersionResponse>(
                DomainError.NotFound("SchemaVersion.NotFound", $"Schema version with ID '{versionId}' was not found."));
        }

        var now = _dateTimeProvider.UtcNow;
        version.Deprecate(now);

        var auditResult = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "EventSchemaVersion.Deprecated",
            "EventSchemaVersion",
            version.Id.ToString(),
            $"{{\"schemaId\":\"{schemaId}\",\"version\":\"{version.Version}\"}}",
            null,
            null,
            now);

        if (auditResult.IsSuccess)
        {
            await _dbContext.AuditEntries.AddAsync(auditResult.Value, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new EventSchemaVersionResponse(
            version.Id,
            version.EventSchemaId,
            version.Version,
            version.VersionNumber,
            version.SchemaJson,
            version.Description,
            version.SamplePayloadJson,
            version.IsActive,
            version.IsDeprecated,
            version.CreatedAt,
            version.UpdatedAt);

        return Result.Success(response);
    }
}
