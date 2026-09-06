using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Schemas;

public sealed class UpdateEventSchemaUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateEventSchemaUseCase(
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

    public async Task<Result<EventSchemaDetailResponse>> ExecuteAsync(
        Guid schemaId,
        UpdateEventSchemaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<EventSchemaDetailResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<EventSchemaDetailResponse>(
                DomainError.Validation("EventSchema.EmptyName", "Schema name cannot be empty."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var schema = await _dbContext.EventSchemas
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Id == schemaId && s.TenantId == tenantId, cancellationToken);

        if (schema == null)
        {
            return Result.Failure<EventSchemaDetailResponse>(
                DomainError.NotFound("EventSchema.NotFound", $"Event schema with ID '{schemaId}' was not found."));
        }

        var now = _dateTimeProvider.UtcNow;
        schema.Update(request.Name, request.Description, request.CompatibilityMode, now);

        if (request.Status.HasValue)
        {
            schema.SetStatus(request.Status.Value, now);
        }

        var auditResult = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "EventSchema.Updated",
            "EventSchema",
            schema.Id.ToString(),
            $"{{\"name\":\"{schema.Name}\",\"compatibilityMode\":\"{schema.CompatibilityMode}\",\"status\":\"{schema.Status}\"}}",
            null,
            null,
            now);

        if (auditResult.IsSuccess)
        {
            await _dbContext.AuditEntries.AddAsync(auditResult.Value, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

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
