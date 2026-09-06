using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.Services;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Schemas;

public sealed class GenerateSchemaDocsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ISchemaCodeGenerator _codeGenerator;

    public GenerateSchemaDocsUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ISchemaCodeGenerator codeGenerator)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _codeGenerator = codeGenerator;
    }

    public async Task<Result<SchemaDocumentationResponse>> ExecuteAsync(
        Guid schemaId,
        Guid? versionId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<SchemaDocumentationResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var schema = await _dbContext.EventSchemas
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Id == schemaId && s.TenantId == tenantId, cancellationToken);

        if (schema == null)
        {
            return Result.Failure<SchemaDocumentationResponse>(
                DomainError.NotFound("EventSchema.NotFound", $"Event schema with ID '{schemaId}' was not found."));
        }

        var targetVersion = versionId.HasValue
            ? schema.Versions.FirstOrDefault(v => v.Id == versionId.Value)
            : schema.Versions.FirstOrDefault(v => v.IsActive) ?? schema.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        if (targetVersion == null)
        {
            return Result.Failure<SchemaDocumentationResponse>(
                DomainError.NotFound("EventSchema.NoVersionFound", "No schema version was found."));
        }

        var docResult = _codeGenerator.GenerateAll(
            schema.EventType,
            schema.Name,
            targetVersion.Version,
            targetVersion.SchemaJson,
            targetVersion.Description);

        var response = new SchemaDocumentationResponse(
            schema.EventType,
            schema.Name,
            targetVersion.Version,
            docResult.MarkdownDocs,
            docResult.TypeScriptSnippet,
            docResult.CSharpSnippet,
            string.IsNullOrWhiteSpace(targetVersion.SamplePayloadJson) ? docResult.SamplePayloadJson : targetVersion.SamplePayloadJson);

        return Result.Success(response);
    }
}
