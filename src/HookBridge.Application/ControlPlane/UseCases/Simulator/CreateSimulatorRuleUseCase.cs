using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed class CreateSimulatorRuleUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateSimulatorRuleUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<SimulatorRuleResponse>> ExecuteAsync(
        CreateSimulatorRuleCommand command,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<SimulatorRuleResponse>(DomainError.Unauthorized("Tenant.Unauthenticated", "Tenant context is required."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var now = _dateTimeProvider.UtcNow;

        var ruleResult = SimulatorRule.Create(
            tenantId,
            command.Name,
            command.Slug,
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
            now
        );

        if (ruleResult.IsFailure)
        {
            return Result.Failure<SimulatorRuleResponse>(ruleResult.Error);
        }

        var rule = ruleResult.Value;

        var slugExists = await _dbContext.SimulatorRules
            .IgnoreQueryFilters()
            .AnyAsync(r => r.Slug == rule.Slug, cancellationToken);

        if (slugExists)
        {
            return Result.Failure<SimulatorRuleResponse>(DomainError.Conflict("SimulatorRule.SlugTaken", $"Simulator rule slug '{rule.Slug}' is already in use."));
        }

        _dbContext.SimulatorRules.Add(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.hookbridge.io" : baseUrl.TrimEnd('/');
        var receiverUrl = $"{effectiveBaseUrl}/api/v1/simulator/receive/{rule.Slug}";

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
            FailureRateObservedPercent: 0.0,
            rule.CreatedAt,
            rule.UpdatedAt
        ));
    }
}
