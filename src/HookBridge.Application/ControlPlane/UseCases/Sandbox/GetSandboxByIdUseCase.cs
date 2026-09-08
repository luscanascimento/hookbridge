using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Sandbox;

public sealed class GetSandboxByIdUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetSandboxByIdUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<WebhookSandboxResponse>> ExecuteAsync(Guid sandboxId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<WebhookSandboxResponse>(DomainError.Unauthorized("Tenant.Unauthenticated", "Tenant context is required."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.hookbridge.io" : baseUrl.TrimEnd('/');

        var sandbox = await _dbContext.WebhookSandboxes
            .AsNoTracking()
            .Where(s => s.Id == sandboxId && s.TenantId == tenantId)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (sandbox == null)
        {
            return Result.Failure<WebhookSandboxResponse>(DomainError.NotFound("WebhookSandbox.NotFound", $"Webhook sandbox '{sandboxId}' was not found."));
        }

        return Result.Success(new WebhookSandboxResponse(
            sandbox.Id,
            sandbox.Name,
            sandbox.Slug,
            $"{effectiveBaseUrl}/api/v1/sandbox/receiver/{sandbox.Slug}",
            sandbox.DefaultResponseStatusCode,
            sandbox.DefaultResponseBody,
            sandbox.DefaultResponseContentType,
            sandbox.DefaultResponseDelayMs,
            sandbox.IsActive,
            sandbox.ExpiresAt,
            sandbox.TotalRequests,
            sandbox.LastRequestAt,
            sandbox.CreatedAt
        ));
    }
}
