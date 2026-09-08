using System.Diagnostics;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Sandbox;

public sealed class ProcessSandboxRequestUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISandboxRealtimeNotifier? _realtimeNotifier;

    public ProcessSandboxRequestUseCase(
        IHookBridgeDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ISandboxRealtimeNotifier? realtimeNotifier = null)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<Result<ProcessSandboxRequestResult>> ExecuteAsync(
        string slug,
        string httpMethod,
        string path,
        string? queryString,
        string headersJson,
        string? body,
        string? contentType,
        long contentLength,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var now = _dateTimeProvider.UtcNow;

        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<ProcessSandboxRequestResult>(DomainError.Validation("WebhookSandbox.InvalidSlug", "Sandbox slug is required."));
        }

        // Public lookup: Ignore tenant global filter
        var sandbox = await _dbContext.WebhookSandboxes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Slug == slug, cancellationToken);

        if (sandbox == null)
        {
            return Result.Failure<ProcessSandboxRequestResult>(DomainError.NotFound("WebhookSandbox.NotFound", $"Webhook sandbox '{slug}' was not found."));
        }

        if (!sandbox.IsActive)
        {
            return Result.Failure<ProcessSandboxRequestResult>(DomainError.Conflict("WebhookSandbox.Inactive", $"Webhook sandbox '{slug}' is currently paused/disabled."));
        }

        if (sandbox.ExpiresAt.HasValue && sandbox.ExpiresAt.Value < now)
        {
            return Result.Failure<ProcessSandboxRequestResult>(DomainError.Conflict("WebhookSandbox.Expired", $"Webhook sandbox '{slug}' has expired."));
        }

        // Simulate configured latency delay if greater than 0
        if (sandbox.DefaultResponseDelayMs > 0)
        {
            await Task.Delay(sandbox.DefaultResponseDelayMs, cancellationToken);
        }

        stopwatch.Stop();
        var durationMs = stopwatch.Elapsed.TotalMilliseconds;

        var requestResult = SandboxRequest.Create(
            sandbox.Id,
            sandbox.TenantId,
            httpMethod,
            path,
            queryString,
            headersJson,
            body,
            contentType,
            contentLength,
            clientIp,
            sandbox.DefaultResponseStatusCode,
            sandbox.DefaultResponseDelayMs,
            now,
            durationMs
        );

        if (requestResult.IsFailure)
        {
            return Result.Failure<ProcessSandboxRequestResult>(requestResult.Error);
        }

        var request = requestResult.Value;
        _dbContext.SandboxRequests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_realtimeNotifier != null)
        {
            await _realtimeNotifier.NotifyRequestCapturedAsync(sandbox, request, cancellationToken);
        }

        return Result.Success(new ProcessSandboxRequestResult(
            sandbox.DefaultResponseStatusCode,
            sandbox.DefaultResponseContentType,
            sandbox.DefaultResponseBody,
            sandbox.DefaultResponseDelayMs
        ));
    }
}
