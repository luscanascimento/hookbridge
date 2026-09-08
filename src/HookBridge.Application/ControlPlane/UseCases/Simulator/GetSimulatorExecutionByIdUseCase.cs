using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed class GetSimulatorExecutionByIdUseCase
{
    private readonly IHookBridgeDbContext _dbContext;

    public GetSimulatorExecutionByIdUseCase(IHookBridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<SimulatorExecutionResponse>> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var execution = await _dbContext.SimulatorExecutions
            .Include(e => e.Rule)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (execution == null)
        {
            return Result.Failure<SimulatorExecutionResponse>(DomainError.NotFound("SimulatorExecution.NotFound", $"Simulator execution '{id}' was not found."));
        }

        return Result.Success(new SimulatorExecutionResponse(
            execution.Id,
            execution.RuleId,
            execution.Rule != null ? execution.Rule.Name : "Ad-Hoc Simulation",
            execution.HttpMethod,
            execution.Path,
            execution.QueryString,
            execution.HeadersJson,
            execution.Body,
            execution.ContentType,
            execution.ContentLength,
            execution.ClientIp,
            execution.InjectedFault,
            execution.SimulatedStatusCode,
            execution.SimulatedDelayMs,
            execution.SimulatedHeadersJson,
            execution.SimulatedResponseBody,
            execution.ExecutionDurationMs,
            execution.ExecutedAt
        ));
    }
}
