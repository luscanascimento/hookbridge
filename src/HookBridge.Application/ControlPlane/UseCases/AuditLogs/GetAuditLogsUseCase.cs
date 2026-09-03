using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.AuditLogs;

public sealed class GetAuditLogsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetAuditLogsUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<PagedList<AuditEntryResponse>>> ExecuteAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<PagedList<AuditEntryResponse>>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var baseQuery = _dbContext.AuditEntries
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.ResourceType))
        {
            var resType = query.ResourceType.Trim();
            baseQuery = baseQuery.Where(a => a.ResourceType == resType);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            baseQuery = baseQuery.Where(a => a.Action == action);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var entries = await baseQuery
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditEntryResponse(
                a.Id,
                a.TenantId,
                a.UserId,
                a.Action,
                a.ResourceType,
                a.ResourceId,
                a.DetailsJson,
                a.IpAddress,
                a.TraceId,
                a.Timestamp))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedList<AuditEntryResponse>(entries, totalCount, page, pageSize));
    }
}
