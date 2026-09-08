using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed class UpdateSimulatorRuleUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateSimulatorRuleUseCase(
        IHookBridgeDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<SimulatorRuleResponse>> ExecuteAsync(
        Guid id,
        UpdateSimulatorRuleCommand command,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        var rule = await _dbContext.SimulatorRules
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (rule == null)
        {
            return Result.Failure<SimulatorRuleResponse>(DomainError.NotFound("SimulatorRule.NotFound", $"Simulator rule with ID '{id}' was not found."));
        }

        var now = _dateTimeProvider.UtcNow;

        var updateResult = rule.Update(
            command.Name,
            command.Strategy,
            command.TargetStatusCode,
            command.SuccessStatusCode,
            command.FailureRatePercent,
            command.FailureStepCount,
            command.DelayMs,
            command.MinDelayMs,
            command.MaxDelayMs,
            command.ResponseHeadersJson,
            command.ResponseBody,
            command.ResponseContentType,
            command.Description,
            command.IsActive,
            now
        );

        if (updateResult.IsFailure)
        {
            return Result.Failure<SimulatorRuleResponse>(updateResult.Error);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

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
