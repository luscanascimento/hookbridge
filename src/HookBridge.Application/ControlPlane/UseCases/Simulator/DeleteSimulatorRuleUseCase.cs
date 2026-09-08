using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed class DeleteSimulatorRuleUseCase
{
    private readonly IHookBridgeDbContext _dbContext;

    public DeleteSimulatorRuleUseCase(IHookBridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await _dbContext.SimulatorRules
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (rule == null)
        {
            return Result.Failure(DomainError.NotFound("SimulatorRule.NotFound", $"Simulator rule with ID '{id}' was not found."));
        }

        _dbContext.SimulatorRules.Remove(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
