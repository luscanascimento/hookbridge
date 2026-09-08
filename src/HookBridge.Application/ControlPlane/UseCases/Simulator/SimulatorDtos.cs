using HookBridge.Domain.Enums;

namespace HookBridge.Application.ControlPlane.UseCases.Simulator;

public sealed record CreateSimulatorRuleCommand(
    string Name,
    string? Slug = null,
    string? Description = null,
    SimulatorStrategy Strategy = SimulatorStrategy.FixedStatus,
    int TargetStatusCode = 500,
    int SuccessStatusCode = 200,
    double FailureRatePercent = 50.0,
    int FailureStepCount = 3,
    int DelayMs = 0,
    int MinDelayMs = 0,
    int MaxDelayMs = 0,
    string? ResponseHeadersJson = null,
    string? ResponseBody = null,
    string ResponseContentType = "application/json"
);

public sealed record UpdateSimulatorRuleCommand(
    string Name,
    string? Description,
    SimulatorStrategy Strategy,
    int TargetStatusCode,
    int SuccessStatusCode,
    double FailureRatePercent,
    int FailureStepCount,
    int DelayMs,
    int MinDelayMs,
    int MaxDelayMs,
    string? ResponseHeadersJson,
    string? ResponseBody,
    string ResponseContentType,
    bool IsActive
);

public sealed record SimulatorRuleResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Slug,
    string ReceiverUrl,
    string? Description,
    SimulatorStrategy Strategy,
    string StrategyName,
    int TargetStatusCode,
    int SuccessStatusCode,
    double FailureRatePercent,
    int FailureStepCount,
    int CurrentStepCount,
    int DelayMs,
    int MinDelayMs,
    int MaxDelayMs,
    string? ResponseHeadersJson,
    string? ResponseBody,
    string ResponseContentType,
    bool IsActive,
    long TotalExecutions,
    long TotalFailures,
    long TotalSuccesses,
    double FailureRateObservedPercent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public sealed record SimulatorExecutionResponse(
    Guid Id,
    Guid? RuleId,
    string? RuleName,
    string HttpMethod,
    string Path,
    string? QueryString,
    string HeadersJson,
    string? Body,
    string? ContentType,
    long ContentLength,
    string? ClientIp,
    string InjectedFault,
    int SimulatedStatusCode,
    int SimulatedDelayMs,
    string? SimulatedHeadersJson,
    string? SimulatedResponseBody,
    double ExecutionDurationMs,
    DateTimeOffset ExecutedAt
);

public sealed record PagedSimulatorExecutionsResponse(
    IReadOnlyList<SimulatorExecutionResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public sealed record SimulatorStatsResponse(
    long TotalExecutions,
    long TotalFailures,
    long TotalSuccesses,
    double OverallFailureRatePercent,
    double AverageLatencyMs,
    int ActiveRulesCount,
    IReadOnlyDictionary<string, int> StatusDistribution,
    IReadOnlyDictionary<string, int> FaultDistribution
);

public sealed record TestDispatchCommand(
    Guid? RuleId = null,
    string? AdHocPreset = null,
    string HttpMethod = "POST",
    string? CustomUrl = null,
    string? HeadersJson = null,
    string? PayloadBody = null,
    int? AdHocStatusCode = null,
    int? AdHocDelayMs = null,
    double? AdHocFailureRate = null,
    int? AdHocRetryAfter = null
);

public sealed record SimulatedExecutionResult(
    int StatusCode,
    int DelayMs,
    Dictionary<string, string> Headers,
    string? Body,
    string ContentType,
    string InjectedFault,
    double DurationMs
);

public sealed record RealtimeSimulatorEvent(
    string EventType,
    Guid TenantId,
    Guid? RuleId,
    string? RuleSlug,
    SimulatorExecutionResponse Execution,
    DateTimeOffset Timestamp
);
