using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.Services;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Schemas;

public sealed class CreateSchemaVersionUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISchemaCompatibilityChecker _compatibilityChecker;

    public CreateSchemaVersionUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ISchemaCompatibilityChecker compatibilityChecker)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _compatibilityChecker = compatibilityChecker;
    }

    public async Task<Result<EventSchemaVersionResponse>> ExecuteAsync(
        Guid schemaId,
        CreateSchemaVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<EventSchemaVersionResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Result.Failure<EventSchemaVersionResponse>(
                DomainError.Validation("SchemaVersion.EmptyVersion", "Version string cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(request.SchemaJson))
        {
            return Result.Failure<EventSchemaVersionResponse>(
                DomainError.Validation("SchemaVersion.EmptySchemaJson", "Schema JSON cannot be empty."));
        }

        try
        {
            using var _ = JsonDocument.Parse(request.SchemaJson);
        }
        catch (JsonException ex)
        {
            return Result.Failure<EventSchemaVersionResponse>(
                DomainError.Validation("SchemaVersion.InvalidJson", $"Invalid JSON Schema: {ex.Message}"));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var schema = await _dbContext.EventSchemas
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Id == schemaId && s.TenantId == tenantId, cancellationToken);

        if (schema == null)
        {
            return Result.Failure<EventSchemaVersionResponse>(
                DomainError.NotFound("EventSchema.NotFound", $"Event schema with ID '{schemaId}' was not found."));
        }

        var normalizedVersion = request.Version.Trim();
        if (schema.Versions.Any(v => string.Equals(v.Version, normalizedVersion, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<EventSchemaVersionResponse>(
                DomainError.Conflict("SchemaVersion.DuplicateVersion", $"Version '{normalizedVersion}' already exists for this schema."));
        }

        var latestVersion = schema.Versions
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        if (latestVersion != null && !request.ForceOverrideCompatibility)
        {
            var compat = _compatibilityChecker.CheckCompatibility(
                latestVersion.SchemaJson,
                request.SchemaJson,
                schema.CompatibilityMode);

            if (!compat.IsCompatible)
            {
                var reasons = string.Join(" | ", compat.BreakingChanges);
                return Result.Failure<EventSchemaVersionResponse>(
                    DomainError.Validation("Schema.Incompatible", $"Schema version breaks {schema.CompatibilityMode} compatibility: {reasons}"));
            }
        }

        var now = _dateTimeProvider.UtcNow;
        var nextNumber = (latestVersion?.VersionNumber ?? 0) + 1;

        if (request.SetActive)
        {
            foreach (var v in schema.Versions.Where(v => v.IsActive))
            {
                v.Deactivate(now);
            }
        }

        var versionResult = EventSchemaVersion.Create(
            tenantId,
            schema.Id,
            normalizedVersion,
            nextNumber,
            request.SchemaJson,
            request.Description,
            request.SamplePayloadJson,
            request.SetActive,
            now);

        if (versionResult.IsFailure)
        {
            return Result.Failure<EventSchemaVersionResponse>(versionResult.Error);
        }

        var newVersion = versionResult.Value;
        await _dbContext.EventSchemaVersions.AddAsync(newVersion, cancellationToken);

        var auditResult = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "EventSchemaVersion.Created",
            "EventSchemaVersion",
            newVersion.Id.ToString(),
            $"{{\"schemaId\":\"{schema.Id}\",\"version\":\"{newVersion.Version}\",\"versionNumber\":{newVersion.VersionNumber}}}",
            null,
            null,
            now);

        if (auditResult.IsSuccess)
        {
            await _dbContext.AuditEntries.AddAsync(auditResult.Value, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new EventSchemaVersionResponse(
            newVersion.Id,
            newVersion.EventSchemaId,
            newVersion.Version,
            newVersion.VersionNumber,
            newVersion.SchemaJson,
            newVersion.Description,
            newVersion.SamplePayloadJson,
            newVersion.IsActive,
            newVersion.IsDeprecated,
            newVersion.CreatedAt,
            newVersion.UpdatedAt);

        return Result.Success(response);
    }
}
