using System.Security.Cryptography;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;

namespace HookBridge.Domain.Entities;

public sealed class SimulatorRule : AggregateRoot<Guid>, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public SimulatorStrategy Strategy { get; private set; } = SimulatorStrategy.FixedStatus;
    public int TargetStatusCode { get; private set; } = 500;
    public int SuccessStatusCode { get; private set; } = 200;
    public double FailureRatePercent { get; private set; } = 50.0;
    public int FailureStepCount { get; private set; } = 3;
    public int CurrentStepCount { get; private set; }
    public int DelayMs { get; private set; }
    public int MinDelayMs { get; private set; }
    public int MaxDelayMs { get; private set; }
    public string? ResponseHeadersJson { get; private set; }
    public string? ResponseBody { get; private set; }
    public string ResponseContentType { get; private set; } = "application/json";
    public bool IsActive { get; private set; } = true;
    public long TotalExecutions { get; private set; }
    public long TotalFailures { get; private set; }
    public long TotalSuccesses { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public ICollection<SimulatorExecution> Executions { get; private set; } = new List<SimulatorExecution>();

    private SimulatorRule() { }

    public static Result<SimulatorRule> Create(
        Guid tenantId,
        string name,
        string? customSlug,
        SimulatorStrategy strategy,
        int targetStatusCode,
        int successStatusCode,
        double failureRatePercent,
        int failureStepCount,
        int delayMs,
        int minDelayMs,
        int maxDelayMs,
        string? responseHeadersJson,
        string? responseBody,
        string responseContentType,
        string? description,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<SimulatorRule>(DomainError.Validation("SimulatorRule.InvalidTenantId", "TenantId cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<SimulatorRule>(DomainError.Validation("SimulatorRule.InvalidName", "Rule name cannot be empty."));
        }

        var slug = string.IsNullOrWhiteSpace(customSlug)
            ? GenerateRandomSlug()
            : SanitizeSlug(customSlug);

        if (string.IsNullOrWhiteSpace(slug) || slug.Length < 4)
        {
            return Result.Failure<SimulatorRule>(DomainError.Validation("SimulatorRule.InvalidSlug", "Rule slug must be at least 4 alphanumeric characters."));
        }

        return Result.Success(new SimulatorRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Slug = slug,
            Description = description?.Trim(),
            Strategy = strategy,
            TargetStatusCode = Math.Clamp(targetStatusCode, 100, 599),
            SuccessStatusCode = Math.Clamp(successStatusCode, 100, 599),
            FailureRatePercent = Math.Clamp(failureRatePercent, 0.0, 100.0),
            FailureStepCount = Math.Max(1, failureStepCount),
            CurrentStepCount = 0,
            DelayMs = Math.Clamp(delayMs, 0, 60000),
            MinDelayMs = Math.Clamp(minDelayMs, 0, 60000),
            MaxDelayMs = Math.Clamp(maxDelayMs, Math.Clamp(minDelayMs, 0, 60000), 60000),
            ResponseHeadersJson = responseHeadersJson,
            ResponseBody = responseBody,
            ResponseContentType = string.IsNullOrWhiteSpace(responseContentType) ? "application/json" : responseContentType.Trim(),
            IsActive = true,
            TotalExecutions = 0,
            TotalFailures = 0,
            TotalSuccesses = 0,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    public Result Update(
        string name,
        SimulatorStrategy strategy,
        int targetStatusCode,
        int successStatusCode,
        double failureRatePercent,
        int failureStepCount,
        int delayMs,
        int minDelayMs,
        int maxDelayMs,
        string? responseHeadersJson,
        string? responseBody,
        string responseContentType,
        string? description,
        bool isActive,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(DomainError.Validation("SimulatorRule.InvalidName", "Rule name cannot be empty."));
        }

        Name = name.Trim();
        Description = description?.Trim();
        Strategy = strategy;
        TargetStatusCode = Math.Clamp(targetStatusCode, 100, 599);
        SuccessStatusCode = Math.Clamp(successStatusCode, 100, 599);
        FailureRatePercent = Math.Clamp(failureRatePercent, 0.0, 100.0);
        FailureStepCount = Math.Max(1, failureStepCount);
        DelayMs = Math.Clamp(delayMs, 0, 60000);
        MinDelayMs = Math.Clamp(minDelayMs, 0, 60000);
        MaxDelayMs = Math.Clamp(maxDelayMs, Math.Clamp(minDelayMs, 0, 60000), 60000);
        ResponseHeadersJson = responseHeadersJson;
        ResponseBody = responseBody;
        ResponseContentType = string.IsNullOrWhiteSpace(responseContentType) ? "application/json" : responseContentType.Trim();
        IsActive = isActive;
        UpdatedAt = now;

        return Result.Success();
    }

    public void RecordExecution(bool isSuccess)
    {
        TotalExecutions++;
        if (isSuccess)
        {
            TotalSuccesses++;
        }
        else
        {
            TotalFailures++;
        }
    }

    public void IncrementStep()
    {
        CurrentStepCount++;
    }

    public void ResetSteps()
    {
        CurrentStepCount = 0;
    }

    public void ToggleActive(bool isActive, DateTimeOffset now)
    {
        IsActive = isActive;
        UpdatedAt = now;
    }

    private static string GenerateRandomSlug()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        return $"sim_{Convert.ToHexStringLower(bytes)}";
    }

    private static string SanitizeSlug(string slug)
    {
        var cleaned = new string(slug.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        return cleaned.StartsWith("sim_", StringComparison.OrdinalIgnoreCase) ? cleaned : $"sim_{cleaned}";
    }
}
