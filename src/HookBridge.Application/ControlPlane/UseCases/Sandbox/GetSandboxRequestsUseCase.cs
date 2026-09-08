using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Sandbox;

public sealed class GetSandboxRequestsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetSandboxRequestsUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<PagedSandboxRequestsResponse>> ExecuteAsync(
        Guid sandboxId,
        string? method = null,
        string? search = null,
        int? statusCode = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<PagedSandboxRequestsResponse>(DomainError.Unauthorized("Tenant.Unauthenticated", "Tenant context is required."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var sandboxExists = await _dbContext.WebhookSandboxes
            .AnyAsync(s => s.Id == sandboxId && s.TenantId == tenantId, cancellationToken);

        if (!sandboxExists)
        {
            return Result.Failure<PagedSandboxRequestsResponse>(DomainError.NotFound("WebhookSandbox.NotFound", $"Webhook sandbox '{sandboxId}' was not found."));
        }

        var query = _dbContext.SandboxRequests
            .AsNoTracking()
            .Where(r => r.SandboxId == sandboxId && r.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(method))
        {
            var upperMethod = method.Trim().ToUpperInvariant();
            query = query.Where(r => r.HttpMethod == upperMethod);
        }

        if (statusCode.HasValue)
        {
            query = query.Where(r => r.ResponseStatusCode == statusCode.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.Path.Contains(term) || (r.Body != null && r.Body.Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var effectivePage = Math.Max(1, page);
        var effectivePageSize = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderByDescending(r => r.ReceivedAt)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(r => new SandboxRequestSummaryResponse(
                r.Id,
                r.SandboxId,
                r.HttpMethod,
                r.Path,
                r.ContentType,
                r.ContentLength,
                r.ClientIp,
                r.ResponseStatusCode,
                r.ResponseDelayMs,
                r.ReceivedAt,
                r.DurationMs
            ))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedSandboxRequestsResponse(items, totalCount, effectivePage, effectivePageSize));
    }
}
