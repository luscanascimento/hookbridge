using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Schemas;

public sealed class CreateEventSchemaUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateEventSchemaUseCase(
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
        CreateEventSchemaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<EventSchemaDetailResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            return Result.Failure<EventSchemaDetailResponse>(
                DomainError.Validation("EventSchema.EmptyEventType", "Event type cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<EventSchemaDetailResponse>(
                DomainError.Validation("EventSchema.EmptyName", "Schema name cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(request.SchemaJson))
        {
            return Result.Failure<EventSchemaDetailResponse>(
                DomainError.Validation("EventSchema.EmptySchemaJson", "Initial schema JSON cannot be empty."));
        }

        try
        {
            using var _ = JsonDocument.Parse(request.SchemaJson);
        }
        catch (JsonException ex)
        {
            return Result.Failure<EventSchemaDetailResponse>(
                DomainError.Validation("EventSchema.InvalidSchemaJson", $"Invalid JSON Schema: {ex.Message}"));
        }

        var normalizedEventType = request.EventType.Trim().ToLowerInvariant();
        var exists = await _dbContext.EventSchemas
            .AnyAsync(s => s.EventType == normalizedEventType, cancellationToken);

        if (exists)
        {
            return Result.Failure<EventSchemaDetailResponse>(
                DomainError.Conflict("EventSchema.DuplicateEventType", $"A schema for event type '{normalizedEventType}' already exists."));
        }

        var now = _dateTimeProvider.UtcNow;
        var schemaResult = EventSchema.Create(
            _tenantContext.TenantId.Value,
            normalizedEventType,
            request.Name,
            request.Description,
            request.CompatibilityMode,
            now);

        if (schemaResult.IsFailure)
        {
            return Result.Failure<EventSchemaDetailResponse>(schemaResult.Error);
        }

        var schema = schemaResult.Value;
        var initialVersion = string.IsNullOrWhiteSpace(request.Version) ? "1.0.0" : request.Version.Trim();

        var versionResult = EventSchemaVersion.Create(
            _tenantContext.TenantId.Value,
            schema.Id,
            initialVersion,
            1,
            request.SchemaJson,
            request.VersionDescription ?? "Initial schema release.",
            request.SamplePayloadJson,
            true,
            now);

        if (versionResult.IsFailure)
        {
            return Result.Failure<EventSchemaDetailResponse>(versionResult.Error);
        }

        var version = versionResult.Value;

        await _dbContext.EventSchemas.AddAsync(schema, cancellationToken);
        await _dbContext.EventSchemaVersions.AddAsync(version, cancellationToken);

        var auditResult = AuditEntry.Create(
            _tenantContext.TenantId.Value,
            _currentUser.UserId,
            "EventSchema.Created",
            "EventSchema",
            schema.Id.ToString(),
            $"{{\"name\":\"{schema.Name}\",\"eventType\":\"{schema.EventType}\",\"version\":\"{version.Version}\"}}",
            null,
            null,
            now);

        if (auditResult.IsSuccess)
        {
            await _dbContext.AuditEntries.AddAsync(auditResult.Value, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var versionResponse = new EventSchemaVersionResponse(
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

        var response = new EventSchemaDetailResponse(
            schema.Id,
            schema.EventType,
            schema.Name,
            schema.Description,
            schema.CompatibilityMode,
            schema.Status,
            new[] { versionResponse },
            versionResponse,
            schema.CreatedAt,
            schema.UpdatedAt);

        return Result.Success(response);
    }
}
