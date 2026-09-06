using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Schemas;

public sealed class GetEventSchemaByIdUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetEventSchemaByIdUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<EventSchemaDetailResponse>> ExecuteAsync(
        Guid schemaId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<EventSchemaDetailResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var schema = await _dbContext.EventSchemas
            .AsNoTracking()
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Id == schemaId && s.TenantId == tenantId, cancellationToken);

        if (schema == null)
        {
            return Result.Failure<EventSchemaDetailResponse>(
                DomainError.NotFound("EventSchema.NotFound", $"Event schema with ID '{schemaId}' was not found."));
        }

        var versions = schema.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new EventSchemaVersionResponse(
                v.Id,
                v.EventSchemaId,
                v.Version,
                v.VersionNumber,
                v.SchemaJson,
                v.Description,
                v.SamplePayloadJson,
                v.IsActive,
                v.IsDeprecated,
                v.CreatedAt,
                v.UpdatedAt))
            .ToList();

        var activeVersion = versions.FirstOrDefault(v => v.IsActive) ?? versions.FirstOrDefault();

        var response = new EventSchemaDetailResponse(
            schema.Id,
            schema.EventType,
            schema.Name,
            schema.Description,
            schema.CompatibilityMode,
            schema.Status,
            versions,
            activeVersion,
            schema.CreatedAt,
            schema.UpdatedAt);

        return Result.Success(response);
    }
}
