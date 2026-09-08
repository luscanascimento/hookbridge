using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Sandbox;

public sealed class GetSandboxesUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetSandboxesUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<WebhookSandboxResponse>>> ExecuteAsync(string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<WebhookSandboxResponse>>(DomainError.Unauthorized("Tenant.Unauthenticated", "Tenant context is required."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.hookbridge.io" : baseUrl.TrimEnd('/');

        var sandboxes = await _dbContext.WebhookSandboxes
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Slug,
                s.DefaultResponseStatusCode,
                s.DefaultResponseBody,
                s.DefaultResponseContentType,
                s.DefaultResponseDelayMs,
                s.IsActive,
                s.ExpiresAt,
                s.CreatedAt,
                TotalRequests = s.Requests.Count,
                LastRequestAt = s.Requests.OrderByDescending(r => r.ReceivedAt).Select(r => (DateTimeOffset?)r.ReceivedAt).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var responses = sandboxes.Select(s => new WebhookSandboxResponse(
            s.Id,
            s.Name,
            s.Slug,
            $"{effectiveBaseUrl}/api/v1/sandbox/receiver/{s.Slug}",
            s.DefaultResponseStatusCode,
            s.DefaultResponseBody,
            s.DefaultResponseContentType,
            s.DefaultResponseDelayMs,
            s.IsActive,
            s.ExpiresAt,
            s.TotalRequests,
            s.LastRequestAt,
            s.CreatedAt
        )).ToList();

        return Result.Success<IReadOnlyList<WebhookSandboxResponse>>(responses);
    }
}
