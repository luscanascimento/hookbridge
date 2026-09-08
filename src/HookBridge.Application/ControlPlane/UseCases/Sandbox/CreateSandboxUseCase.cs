using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Sandbox;

public sealed class CreateSandboxUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateSandboxUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<WebhookSandboxResponse>> ExecuteAsync(CreateSandboxCommand command, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<WebhookSandboxResponse>(DomainError.Unauthorized("Tenant.Unauthenticated", "Tenant context is required."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var now = _dateTimeProvider.UtcNow;
        DateTimeOffset? expiresAt = command.TtlHours.HasValue && command.TtlHours.Value > 0
            ? now.AddHours(command.TtlHours.Value)
            : null;

        var sandboxResult = WebhookSandbox.Create(
            tenantId,
            command.Name,
            command.CustomSlug,
            now,
            command.DefaultStatusCode ?? 200,
            command.DefaultBody,
            command.DefaultContentType ?? "application/json",
            command.DefaultDelayMs ?? 0,
            expiresAt
        );

        if (sandboxResult.IsFailure)
        {
            return Result.Failure<WebhookSandboxResponse>(sandboxResult.Error);
        }

        var sandbox = sandboxResult.Value;

        // Ensure unique slug
        var slugExists = await _dbContext.WebhookSandboxes
            .IgnoreQueryFilters()
            .AnyAsync(s => s.Slug == sandbox.Slug, cancellationToken);

        if (slugExists)
        {
            return Result.Failure<WebhookSandboxResponse>(DomainError.Conflict("WebhookSandbox.SlugTaken", $"Sandbox slug '{sandbox.Slug}' is already in use."));
        }

        _dbContext.WebhookSandboxes.Add(sandbox);
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
            TotalRequestsCount: 0,
            LastRequestAt: null,
            sandbox.CreatedAt
        ));
    }
}
