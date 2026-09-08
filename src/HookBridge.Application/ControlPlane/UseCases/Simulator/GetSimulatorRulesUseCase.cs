using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed class GetSimulatorRulesUseCase
{
    private readonly IHookBridgeDbContext _dbContext;

    public GetSimulatorRulesUseCase(IHookBridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<SimulatorRuleResponse>>> ExecuteAsync(
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.hookbridge.io" : baseUrl.TrimEnd('/');

        var rules = await _dbContext.SimulatorRules
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var responses = rules.Select(rule =>
        {
            var receiverUrl = $"{effectiveBaseUrl}/api/v1/simulator/receive/{rule.Slug}";
            var observedRate = rule.TotalExecutions > 0
                ? Math.Round((double)rule.TotalFailures / rule.TotalExecutions * 100.0, 1)
                : 0.0;

            return new SimulatorRuleResponse(
                rule.Id,
                rule.TenantId,
                rule.Name,
                rule.Slug,
                receiverUrl,
                rule.Description,
                rule.Strategy,
                rule.Strategy.ToString(),
                rule.TargetStatusCode,
                rule.SuccessStatusCode,
                rule.FailureRatePercent,
                rule.FailureStepCount,
                rule.CurrentStepCount,
                rule.DelayMs,
                rule.MinDelayMs,
                rule.MaxDelayMs,
                rule.ResponseHeadersJson,
                rule.ResponseBody,
                rule.ResponseContentType,
                rule.IsActive,
                rule.TotalExecutions,
                rule.TotalFailures,
                rule.TotalSuccesses,
                observedRate,
                rule.CreatedAt,
                rule.UpdatedAt
            );
        }).ToList();

        return Result.Success<IReadOnlyList<SimulatorRuleResponse>>(responses);
    }
}
