using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed class TestDispatchSimulatorUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ProcessSimulatorRequestUseCase _processRuleUseCase;
    private readonly ExecuteAdHocSimulationUseCase _processAdHocUseCase;

    public TestDispatchSimulatorUseCase(
        IHookBridgeDbContext dbContext,
        ProcessSimulatorRequestUseCase processRuleUseCase,
        ExecuteAdHocSimulationUseCase processAdHocUseCase)
    {
        _dbContext = dbContext;
        _processRuleUseCase = processRuleUseCase;
        _processAdHocUseCase = processAdHocUseCase;
    }

    public async Task<Result<SimulatedExecutionResult>> ExecuteAsync(
        TestDispatchCommand command,
        CancellationToken cancellationToken = default)
    {
        var headersJson = command.HeadersJson ?? "{\"User-Agent\": \"HookBridge-TestDispatcher/1.0\"}";
        var body = command.PayloadBody ?? "{\"event\": \"test.ping\", \"timestamp\": \"" + DateTimeOffset.UtcNow.ToString("o") + "\"}";
        var contentType = "application/json";
        var contentLength = System.Text.Encoding.UTF8.GetByteCount(body);
        var path = command.CustomUrl ?? "/api/v1/simulator/test-dispatch";

        if (command.RuleId.HasValue)
        {
            var rule = await _dbContext.SimulatorRules
                .FirstOrDefaultAsync(r => r.Id == command.RuleId.Value, cancellationToken);

            if (rule == null)
            {
                return Result.Failure<SimulatedExecutionResult>(DomainError.NotFound("SimulatorRule.NotFound", $"Simulator rule '{command.RuleId.Value}' not found."));
            }

            return await _processRuleUseCase.ExecuteAsync(
                rule.Slug,
                command.HttpMethod,
                path,
                queryString: null,
                headersJson,
                body,
                contentType,
                contentLength,
                clientIp: "127.0.0.1",
                cancellationToken
            );
        }

        return await _processAdHocUseCase.ExecuteAsync(
            command.AdHocStatusCode,
            command.AdHocDelayMs,
            command.AdHocFailureRate,
            command.AdHocRetryAfter,
            customBodyParam: null,
            command.HttpMethod,
            path,
            queryString: null,
            headersJson,
            body,
            contentType,
            contentLength,
            clientIp: "127.0.0.1",
            cancellationToken
        );
    }
}
