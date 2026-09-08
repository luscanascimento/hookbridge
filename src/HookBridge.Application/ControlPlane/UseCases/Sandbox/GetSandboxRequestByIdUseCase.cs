using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Sandbox;

public sealed class GetSandboxRequestByIdUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetSandboxRequestByIdUseCase(IHookBridgeDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<SandboxRequestResponse>> ExecuteAsync(
        Guid sandboxId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<SandboxRequestResponse>(DomainError.Unauthorized("Tenant.Unauthenticated", "Tenant context is required."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var request = await _dbContext.SandboxRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.SandboxId == sandboxId && r.TenantId == tenantId, cancellationToken);

        if (request == null)
        {
            return Result.Failure<SandboxRequestResponse>(DomainError.NotFound("SandboxRequest.NotFound", $"Captured sandbox request '{requestId}' was not found."));
        }

        return Result.Success(new SandboxRequestResponse(
            request.Id,
            request.SandboxId,
            request.HttpMethod,
            request.Path,
            request.QueryString,
            request.HeadersJson,
            request.Body,
            request.ContentType,
            request.ContentLength,
            request.ClientIp,
            request.ResponseStatusCode,
            request.ResponseDelayMs,
            request.ReceivedAt,
            request.DurationMs
        ));
    }
}
