using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed class GetSimulatorRuleByIdUseCase
{
    private readonly IHookBridgeDbContext _dbContext;

    public GetSimulatorRuleByIdUseCase(IHookBridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<SimulatorRuleResponse>> ExecuteAsync(
        Guid id,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        var rule = await _dbContext.SimulatorRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (rule == null)
        {
            return Result.Failure<SimulatorRuleResponse>(DomainError.NotFound("SimulatorRule.NotFound", $"Simulator rule with ID '{id}' was not found."));
        }

        var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.hookbridge.io" : baseUrl.TrimEnd('/');
        var receiverUrl = $"{effectiveBaseUrl}/api/v1/simulator/receive/{rule.Slug}";
        var observedRate = rule.TotalExecutions > 0
            ? Math.Round((double)rule.TotalFailures / rule.TotalExecutions * 100.0, 1)
            : 0.0;

        return Result.Success(new SimulatorRuleResponse(
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
        ));
    }
}
