using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Sandbox;

public sealed class UpdateSandboxConfigUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateSandboxConfigUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<WebhookSandboxResponse>> ExecuteAsync(
        Guid sandboxId,
        UpdateSandboxConfigCommand command,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<WebhookSandboxResponse>(DomainError.Unauthorized("Tenant.Unauthenticated", "Tenant context is required."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var sandbox = await _dbContext.WebhookSandboxes
            .Include(s => s.Requests)
            .FirstOrDefaultAsync(s => s.Id == sandboxId && s.TenantId == tenantId, cancellationToken);

        if (sandbox == null)
        {
            return Result.Failure<WebhookSandboxResponse>(DomainError.NotFound("WebhookSandbox.NotFound", $"Webhook sandbox '{sandboxId}' was not found."));
        }

        var now = _dateTimeProvider.UtcNow;
        var updateResult = sandbox.UpdateConfig(
            command.Name,
            command.DefaultStatusCode,
            command.DefaultBody,
            command.DefaultContentType,
            command.DefaultDelayMs,
            command.IsActive,
            now
        );

        if (updateResult.IsFailure)
        {
            return Result.Failure<WebhookSandboxResponse>(updateResult.Error);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.hookbridge.io" : baseUrl.TrimEnd('/');
        var receiverUrl = $"{effectiveBaseUrl}/api/v1/sandbox/receiver/{sandbox.Slug}";

        return Result.Success(new WebhookSandboxResponse(
            sandbox.Id,
            sandbox.Name,
            sandbox.Slug,
            receiverUrl,
            sandbox.DefaultResponseStatusCode,
            sandbox.DefaultResponseBody,
            sandbox.DefaultResponseContentType,
            sandbox.DefaultResponseDelayMs,
            sandbox.IsActive,
            sandbox.ExpiresAt,
            sandbox.Requests.Count,
            sandbox.Requests.OrderByDescending(r => r.ReceivedAt).Select(r => (DateTimeOffset?)r.ReceivedAt).FirstOrDefault(),
            sandbox.CreatedAt
        ));
    }
}
