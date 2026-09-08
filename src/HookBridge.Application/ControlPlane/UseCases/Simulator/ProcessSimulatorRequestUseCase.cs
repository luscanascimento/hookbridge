using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed class ProcessSimulatorRequestUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISimulatorRealtimeNotifier? _realtimeNotifier;

    public ProcessSimulatorRequestUseCase(
        IHookBridgeDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ISimulatorRealtimeNotifier? realtimeNotifier = null)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<Result<SimulatedExecutionResult>> ExecuteAsync(
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
            return Result.Failure<SimulatedExecutionResult>(DomainError.Validation("SimulatorRule.InvalidSlug", "Rule slug is required."));
        }

        // Public lookup: Ignore tenant global filter
        var rule = await _dbContext.SimulatorRules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Slug == slug, cancellationToken);

        if (rule == null)
        {
            return Result.Failure<SimulatedExecutionResult>(DomainError.NotFound("SimulatorRule.NotFound", $"Simulator rule '{slug}' was not found."));
        }

        if (!rule.IsActive)
        {
            return Result.Failure<SimulatedExecutionResult>(DomainError.Conflict("SimulatorRule.Inactive", $"Simulator rule '{slug}' is currently paused/disabled."));
        }

        int statusCode;
        int delayMs = 0;
        bool isSuccess;
        string injectedFault;
        string? responseBody;
        var responseContentType = rule.ResponseContentType;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Evaluate Strategy
        switch (rule.Strategy)
        {
            case SimulatorStrategy.FixedStatus:
                statusCode = rule.TargetStatusCode;
                delayMs = rule.DelayMs;
                isSuccess = statusCode >= 200 && statusCode < 300;
                injectedFault = $"Fixed HTTP {statusCode}";
                responseBody = rule.ResponseBody ?? (isSuccess
                    ? "{\"status\": \"ok\", \"simulated\": true}"
                    : $"{{\"error\": \"Simulated failure\", \"code\": {statusCode}}}");
                break;

            case SimulatorStrategy.FailureRate:
                var randomPercent = Random.Shared.NextDouble() * 100.0;
                var shouldFail = randomPercent < rule.FailureRatePercent;
                statusCode = shouldFail ? rule.TargetStatusCode : rule.SuccessStatusCode;
                isSuccess = !shouldFail;
                delayMs = rule.DelayMs;
                injectedFault = shouldFail
                    ? string.Create(CultureInfo.InvariantCulture, $"Failure Rate Fault ({rule.FailureRatePercent:F0}%) -> HTTP {statusCode}")
                    : string.Create(CultureInfo.InvariantCulture, $"Failure Rate Success ({100 - rule.FailureRatePercent:F0}%) -> HTTP {statusCode}");
                responseBody = rule.ResponseBody ?? (isSuccess
                    ? "{\"status\": \"ok\", \"simulated\": true}"
                    : $"{{\"error\": \"Simulated failure rate fault\", \"code\": {statusCode}}}");
                break;

            case SimulatorStrategy.SequentialRetryPattern:
                var currentStep = rule.CurrentStepCount;
                var isStepFailure = currentStep < rule.FailureStepCount;
                statusCode = isStepFailure ? rule.TargetStatusCode : rule.SuccessStatusCode;
                isSuccess = !isStepFailure;
                delayMs = rule.DelayMs;
                injectedFault = isStepFailure
                    ? string.Create(CultureInfo.InvariantCulture, $"Sequential Retry Step {currentStep + 1}/{rule.FailureStepCount} -> HTTP {statusCode}")
                    : string.Create(CultureInfo.InvariantCulture, $"Sequential Retry Success (Recovered after {rule.FailureStepCount} attempts) -> HTTP {statusCode}");
                rule.IncrementStep();
                responseBody = rule.ResponseBody ?? (isSuccess
                    ? "{\"status\": \"recovered\", \"simulated\": true}"
                    : $"{{\"error\": \"Simulated transient step failure\", \"attempt\": {currentStep + 1}}}");
                break;

            case SimulatorStrategy.Timeout:
                statusCode = rule.TargetStatusCode == 200 ? 504 : rule.TargetStatusCode;
                delayMs = rule.DelayMs > 0 ? rule.DelayMs : 5000;
                isSuccess = false;
                injectedFault = string.Create(CultureInfo.InvariantCulture, $"Simulated Timeout ({delayMs}ms) -> HTTP {statusCode}");
                responseBody = rule.ResponseBody ?? $"{{\"error\": \"Gateway Timeout\", \"message\": \"Simulated latency delay of {delayMs}ms induced by HookBridge Simulator\"}}";
                break;

            case SimulatorStrategy.ChaosJitter:
                var min = Math.Min(rule.MinDelayMs, rule.MaxDelayMs);
                var max = Math.Max(rule.MinDelayMs, rule.MaxDelayMs);
                delayMs = max > min ? Random.Shared.Next(min, max + 1) : (min > 0 ? min : 350);
                int[] errorPool = [429, 500, 502, 503, 504];
                statusCode = errorPool[Random.Shared.Next(errorPool.Length)];
                isSuccess = false;
                injectedFault = string.Create(CultureInfo.InvariantCulture, $"Chaos Jitter ({delayMs}ms) -> HTTP {statusCode}");
                responseBody = rule.ResponseBody ?? $"{{\"error\": \"Chaos fault injection\", \"code\": {statusCode}, \"jitter_ms\": {delayMs}}}";
                break;

            case SimulatorStrategy.MalformedJson:
                statusCode = rule.TargetStatusCode;
                delayMs = rule.DelayMs;
                isSuccess = false;
                injectedFault = "Malformed / Corrupted JSON Payload";
                responseContentType = "application/json";
                responseBody = "{ \"status\": \"error\", \"data\": [\"corrupted_unterminated_json_stream_payload";
                break;

            default:
                statusCode = 200;
                isSuccess = true;
                injectedFault = "Default 200 OK";
                responseBody = "{\"status\": \"ok\"}";
                break;
        }

        // Custom headers parsing
        if (!string.IsNullOrWhiteSpace(rule.ResponseHeadersJson))
        {
            try
            {
                var customHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(rule.ResponseHeadersJson);
                if (customHeaders != null)
                {
                    foreach (var (k, v) in customHeaders)
                    {
                        headers[k] = v;
                    }
                }
            }
            catch
            {
                // Fallback ignore invalid JSON in headers
            }
        }

        // Automatic 429 headers if not specified
        if (statusCode == 429 && !headers.ContainsKey("Retry-After"))
        {
            headers["Retry-After"] = "30";
            headers["X-RateLimit-Limit"] = "100";
            headers["X-RateLimit-Remaining"] = "0";
            headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        }

        // Apply delay simulation
        if (delayMs > 0)
        {
            await Task.Delay(delayMs, cancellationToken);
        }

        stopwatch.Stop();
        var durationMs = stopwatch.Elapsed.TotalMilliseconds;

        var headersJsonSerialized = JsonSerializer.Serialize(headers);

        var executionResult = SimulatorExecution.Create(
            rule.Id,
            rule.TenantId,
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

        if (executionResult.IsFailure)
        {
            return Result.Failure<SimulatedExecutionResult>(executionResult.Error);
        }

        var execution = executionResult.Value;
        rule.RecordExecution(isSuccess);
        _dbContext.SimulatorExecutions.Add(execution);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_realtimeNotifier != null)
        {
            await _realtimeNotifier.NotifyExecutionCapturedAsync(rule, execution, cancellationToken);
        }

        return Result.Success(new SimulatedExecutionResult(
            statusCode,
            delayMs,
            headers,
            responseBody,
            responseContentType,
            injectedFault,
            durationMs
        ));
    }
}
