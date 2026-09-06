using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Schemas;

public sealed class GetEventSchemasUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetEventSchemasUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<EventSchemaSummaryResponse>>> ExecuteAsync(
        string? search = null,
        SchemaStatus? status = null,
        SchemaCompatibilityMode? compatibilityMode = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<EventSchemaSummaryResponse>>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var query = _dbContext.EventSchemas
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(s => s.EventType.Contains(term) || s.Name.Contains(term));
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        if (compatibilityMode.HasValue)
        {
            query = query.Where(s => s.CompatibilityMode == compatibilityMode.Value);
        }

        var schemas = await query
            .OrderBy(s => s.EventType)
            .Include(s => s.Versions)
            .ToListAsync(cancellationToken);

        var response = schemas.Select(s =>
        {
            var activeVersion = s.Versions
                .Where(v => v.IsActive)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault()?.Version ?? s.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault()?.Version;

            return new EventSchemaSummaryResponse(
                s.Id,
                s.EventType,
                s.Name,
                s.Description,
                s.CompatibilityMode,
                s.Status,
                s.Versions.Count,
                activeVersion,
                s.CreatedAt,
                s.UpdatedAt);
        }).ToList();

        return Result.Success<IReadOnlyList<EventSchemaSummaryResponse>>(response);
    }
}
