using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed class ExecuteAdHocSimulationUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITenantContext _tenantContext;
    private readonly ISimulatorRealtimeNotifier? _realtimeNotifier;

    public ExecuteAdHocSimulationUseCase(
        IHookBridgeDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ITenantContext tenantContext,
        ISimulatorRealtimeNotifier? realtimeNotifier = null)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _tenantContext = tenantContext;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<Result<SimulatedExecutionResult>> ExecuteAsync(
        int? requestedStatusCode,
        int? delayMsParam,
        double? failureRateParam,
        int? retryAfterParam,
        string? customBodyParam,
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

        int statusCode;
        int delayMs = Math.Clamp(delayMsParam ?? 0, 0, 60000);
        string injectedFault;
        string? responseBody;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (failureRateParam.HasValue)
        {
            var rate = Math.Clamp(failureRateParam.Value, 0.0, 100.0);
            var rand = Random.Shared.NextDouble() * 100.0;
            var shouldFail = rand < rate;
            if (shouldFail)
            {
                statusCode = requestedStatusCode ?? 500;
                injectedFault = string.Create(CultureInfo.InvariantCulture, $"Ad-Hoc Failure Rate Fault ({rate:F0}%) -> HTTP {statusCode}");
                responseBody = customBodyParam ?? $"{{\"error\": \"Ad-Hoc Chaos Flakiness Fault\", \"code\": {statusCode}}}";
            }
            else
            {
                statusCode = 200;
                injectedFault = string.Create(CultureInfo.InvariantCulture, $"Ad-Hoc Failure Rate Success ({100 - rate:F0}%) -> HTTP 200");
                responseBody = customBodyParam ?? "{\"status\": \"ok\", \"ad_hoc_simulated\": true}";
            }
        }
        else if (requestedStatusCode.HasValue)
        {
            statusCode = Math.Clamp(requestedStatusCode.Value, 100, 599);
            injectedFault = $"Ad-Hoc HTTP {statusCode}";
            var isSuccess = statusCode >= 200 && statusCode < 300;
            responseBody = customBodyParam ?? (isSuccess
                ? "{\"status\": \"ok\", \"ad_hoc_simulated\": true}"
                : $"{{\"error\": \"Simulated ad-hoc error\", \"code\": {statusCode}}}");
        }
        else
        {
            statusCode = 200;
            injectedFault = "Ad-Hoc Default 200 OK";
            responseBody = customBodyParam ?? "{\"status\": \"ok\", \"ad_hoc_simulated\": true}";
        }

        if (statusCode == 429 || retryAfterParam.HasValue)
        {
            var retrySec = retryAfterParam ?? 30;
            headers["Retry-After"] = retrySec.ToString(CultureInfo.InvariantCulture);
            headers["X-RateLimit-Limit"] = "100";
            headers["X-RateLimit-Remaining"] = "0";
            headers["X-RateLimit-Reset"] = now.AddSeconds(retrySec).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        }

        if (delayMs > 0)
        {
            await Task.Delay(delayMs, cancellationToken);
        }

        stopwatch.Stop();
        var durationMs = stopwatch.Elapsed.TotalMilliseconds;

        // Resolve Tenant
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            // Pick first tenant for recording if running in multi-tenant background without auth header
            var firstTenant = await _dbContext.Tenants.IgnoreQueryFilters().Select(t => t.Id).FirstOrDefaultAsync(cancellationToken);
            tenantId = firstTenant != Guid.Empty ? firstTenant : Guid.NewGuid();
        }

        var headersJsonSerialized = JsonSerializer.Serialize(headers);

        var executionResult = SimulatorExecution.Create(
            ruleId: null,
            tenantId.Value,
            httpMethod,
            path,
            queryString,
            headersJson,
            body,
            contentType,
            contentLength,
            clientIp,
            injectedFault,
            statusCode,
            delayMs,
            headersJsonSerialized,
            responseBody,
            durationMs,
            now
        );

        if (executionResult.IsSuccess)
        {
            var execution = executionResult.Value;
            _dbContext.SimulatorExecutions.Add(execution);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (_realtimeNotifier != null)
            {
                await _realtimeNotifier.NotifyExecutionCapturedAsync(null, execution, cancellationToken);
            }
        }

        return Result.Success(new SimulatedExecutionResult(
            statusCode,
            delayMs,
            headers,
            responseBody,
            "application/json",
            injectedFault,
            durationMs
        ));
    }
}
