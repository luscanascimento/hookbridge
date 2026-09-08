using System.Globalization;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed class GetSimulatorExecutionsUseCase
{
    private readonly IHookBridgeDbContext _dbContext;

    public GetSimulatorExecutionsUseCase(IHookBridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedSimulatorExecutionsResponse>> ExecuteAsync(
        Guid? ruleId = null,
        int? statusCode = null,
        string? method = null,
        string? search = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SimulatorExecutions
            .Include(e => e.Rule)
            .AsNoTracking();

        if (ruleId.HasValue)
        {
            query = query.Where(e => e.RuleId == ruleId.Value);
        }

        if (statusCode.HasValue)
        {
            query = query.Where(e => e.SimulatedStatusCode == statusCode.Value);
        }

        if (!string.IsNullOrWhiteSpace(method))
        {
            var m = method.Trim().ToUpperInvariant();
            query = query.Where(e => e.HttpMethod == m);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(e =>
                e.Path.Contains(s) ||
                e.InjectedFault.Contains(s) ||
                (e.Body != null && e.Body.Contains(s)) ||
                (e.Rule != null && e.Rule.Name.Contains(s))
            );
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var effectivePage = Math.Max(1, page);
        var effectivePageSize = Math.Clamp(pageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalCount / (double)effectivePageSize);

        var items = await query
            .OrderByDescending(e => e.ExecutedAt)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(e => new SimulatorExecutionResponse(
                e.Id,
                e.RuleId,
                e.Rule != null ? e.Rule.Name : "Ad-Hoc Simulation",
                e.HttpMethod,
                e.Path,
                e.QueryString,
                e.HeadersJson,
                e.Body,
                e.ContentType,
                e.ContentLength,
                e.ClientIp,
                e.InjectedFault,
                e.SimulatedStatusCode,
                e.SimulatedDelayMs,
                e.SimulatedHeadersJson,
                e.SimulatedResponseBody,
                e.ExecutionDurationMs,
                e.ExecutedAt
            ))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedSimulatorExecutionsResponse(
            items,
            totalCount,
            effectivePage,
            effectivePageSize,
            totalPages
        ));
    }
}
