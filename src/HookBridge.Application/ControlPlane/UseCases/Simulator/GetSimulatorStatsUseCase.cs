using System.Globalization;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed class GetSimulatorStatsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;

    public GetSimulatorStatsUseCase(IHookBridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<SimulatorStatsResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var executions = await _dbContext.SimulatorExecutions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var activeRulesCount = await _dbContext.SimulatorRules
            .CountAsync(r => r.IsActive, cancellationToken);

        var totalExecutions = executions.Count;
        var totalFailures = executions.Count(e => e.SimulatedStatusCode >= 400);
        var totalSuccesses = executions.Count(e => e.SimulatedStatusCode >= 200 && e.SimulatedStatusCode < 300);

        var failureRate = totalExecutions > 0
            ? Math.Round((double)totalFailures / totalExecutions * 100.0, 1)
            : 0.0;

        var avgLatency = totalExecutions > 0
            ? Math.Round(executions.Average(e => e.ExecutionDurationMs), 1)
            : 0.0;

        var statusDist = executions
            .GroupBy(e => e.SimulatedStatusCode.ToString(CultureInfo.InvariantCulture))
            .ToDictionary(g => g.Key, g => g.Count());

        var faultDist = executions
            .GroupBy(e => GetFaultCategory(e.InjectedFault))
            .ToDictionary(g => g.Key, g => g.Count());

        return Result.Success(new SimulatorStatsResponse(
            totalExecutions,
            totalFailures,
            totalSuccesses,
            failureRate,
            avgLatency,
            activeRulesCount,
            statusDist,
            faultDist
        ));
    }

    private static string GetFaultCategory(string fault)
    {
        if (string.IsNullOrWhiteSpace(fault)) return "Other";
        if (fault.Contains("Rate", StringComparison.OrdinalIgnoreCase) || fault.Contains("429", StringComparison.OrdinalIgnoreCase)) return "Rate Limiting";
        if (fault.Contains("Timeout", StringComparison.OrdinalIgnoreCase) || fault.Contains("504", StringComparison.OrdinalIgnoreCase)) return "Timeouts";
        if (fault.Contains("Sequential", StringComparison.OrdinalIgnoreCase)) return "Sequential Backoff";
        if (fault.Contains("Chaos", StringComparison.OrdinalIgnoreCase) || fault.Contains("Jitter", StringComparison.OrdinalIgnoreCase)) return "Chaos & Jitter";
        if (fault.Contains("Malformed", StringComparison.OrdinalIgnoreCase)) return "Malformed Payloads";
        if (fault.Contains("500", StringComparison.OrdinalIgnoreCase) || fault.Contains("502", StringComparison.OrdinalIgnoreCase) || fault.Contains("503", StringComparison.OrdinalIgnoreCase)) return "Server Errors";
        if (fault.Contains("200", StringComparison.OrdinalIgnoreCase) || fault.Contains("Success", StringComparison.OrdinalIgnoreCase)) return "Success";
        return "Custom";
    }
}
