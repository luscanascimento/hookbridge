using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Schemas;

public sealed class ActivateSchemaVersionUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ActivateSchemaVersionUseCase(
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
        var versions = await _dbContext.EventSchemaVersions
            .Where(v => v.EventSchemaId == schemaId && v.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var targetVersion = versions.FirstOrDefault(v => v.Id == versionId);
        if (targetVersion == null)
        {
            return Result.Failure<EventSchemaVersionResponse>(
                DomainError.NotFound("SchemaVersion.NotFound", $"Schema version with ID '{versionId}' was not found."));
        }

        var now = _dateTimeProvider.UtcNow;
        foreach (var v in versions)
        {
            if (v.Id == targetVersion.Id)
            {
                v.Activate(now);
            }
            else
            {
                v.Deactivate(now);
            }
        }

        var auditResult = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "EventSchemaVersion.Activated",
            "EventSchemaVersion",
            targetVersion.Id.ToString(),
            $"{{\"schemaId\":\"{schemaId}\",\"version\":\"{targetVersion.Version}\"}}",
            null,
            null,
            now);

        if (auditResult.IsSuccess)
        {
            await _dbContext.AuditEntries.AddAsync(auditResult.Value, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new EventSchemaVersionResponse(
            targetVersion.Id,
            targetVersion.EventSchemaId,
            targetVersion.Version,
            targetVersion.VersionNumber,
            targetVersion.SchemaJson,
            targetVersion.Description,
            targetVersion.SamplePayloadJson,
            targetVersion.IsActive,
            targetVersion.IsDeprecated,
            targetVersion.CreatedAt,
            targetVersion.UpdatedAt);

        return Result.Success(response);
    }
}
