using HookBridge.Domain.Common;

namespace HookBridge.Domain.Entities;

public sealed class SimulatorExecution : Entity<Guid>, ITenantScoped
{
    public Guid? RuleId { get; private set; }
    public Guid TenantId { get; private set; }
    public string HttpMethod { get; private set; } = "POST";
    public string Path { get; private set; } = string.Empty;
    public string? QueryString { get; private set; }
    public string HeadersJson { get; private set; } = "{}";
    public string? Body { get; private set; }
    public string? ContentType { get; private set; }
    public long ContentLength { get; private set; }
    public string? ClientIp { get; private set; }
    public string InjectedFault { get; private set; } = string.Empty;
    public int SimulatedStatusCode { get; private set; }
    public int SimulatedDelayMs { get; private set; }
    public string? SimulatedHeadersJson { get; private set; }
    public string? SimulatedResponseBody { get; private set; }
    public double ExecutionDurationMs { get; private set; }
    public DateTimeOffset ExecutedAt { get; private set; }

    public SimulatorRule? Rule { get; private set; }

    private SimulatorExecution() { }

    public static Result<SimulatorExecution> Create(
        Guid? ruleId,
        Guid tenantId,
        string httpMethod,
        string path,
        string? queryString,
        string headersJson,
        string? body,
        string? contentType,
        long contentLength,
        string? clientIp,
        string injectedFault,
        int simulatedStatusCode,
        int simulatedDelayMs,
        string? simulatedHeadersJson,
        string? simulatedResponseBody,
        double executionDurationMs,
        DateTimeOffset executedAt)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<SimulatorExecution>(DomainError.Validation("SimulatorExecution.InvalidTenantId", "TenantId cannot be empty."));
        }

        return Result.Success(new SimulatorExecution
        {
            Id = Guid.NewGuid(),
            RuleId = ruleId,
            TenantId = tenantId,
            HttpMethod = string.IsNullOrWhiteSpace(httpMethod) ? "POST" : httpMethod.ToUpperInvariant(),
            Path = path ?? string.Empty,
            QueryString = queryString,
            HeadersJson = string.IsNullOrWhiteSpace(headersJson) ? "{}" : headersJson,
            Body = body,
            ContentType = contentType,
            ContentLength = Math.Max(0, contentLength),
            ClientIp = clientIp,
            InjectedFault = string.IsNullOrWhiteSpace(injectedFault) ? "Simulation" : injectedFault.Trim(),
            SimulatedStatusCode = simulatedStatusCode,
            SimulatedDelayMs = Math.Max(0, simulatedDelayMs),
            SimulatedHeadersJson = simulatedHeadersJson,
            SimulatedResponseBody = simulatedResponseBody,
            ExecutionDurationMs = Math.Max(0, executionDurationMs),
            ExecutedAt = executedAt
        });
    }
}
