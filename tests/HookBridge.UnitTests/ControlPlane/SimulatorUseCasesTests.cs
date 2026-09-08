using System.Globalization;
using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.UseCases.Simulator;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class SimulatorUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly DateTimeProvider _dt;
    private readonly Guid _tenantId;

    public SimulatorUseCasesTests()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "test-simulator-tenant");

        _db = new HookBridgeDbContext(options, _tenantContext);
        _dt = new DateTimeProvider();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task CreateSimulatorRule_ShouldCreateRule_WithConfiguredStrategyAndReceiverUrl()
    {
        // Arrange
        var useCase = new CreateSimulatorRuleUseCase(_db, _tenantContext, _dt);
        var command = new CreateSimulatorRuleCommand(
            Name: "Payment Gateway 429 Throttle",
            Slug: "payment-throttle",
            Description: "Simulates rate limiting",
            Strategy: SimulatorStrategy.FixedStatus,
            TargetStatusCode: 429,
            SuccessStatusCode: 200,
            FailureRatePercent: 0,
            FailureStepCount: 1,
            DelayMs: 50,
            ResponseHeadersJson: "{\"Retry-After\": \"60\"}",
            ResponseBody: "{\"error\": \"Rate limit exceeded\"}",
            ResponseContentType: "application/json"
        );

        // Act
        var result = await useCase.ExecuteAsync(command, "https://api.hookbridge.io");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Payment Gateway 429 Throttle");
        result.Value.Slug.Should().Be("sim_payment-throttle");
        result.Value.ReceiverUrl.Should().Be("https://api.hookbridge.io/api/v1/simulator/receive/sim_payment-throttle");
        result.Value.Strategy.Should().Be(SimulatorStrategy.FixedStatus);
        result.Value.TargetStatusCode.Should().Be(429);
        result.Value.DelayMs.Should().Be(50);
        result.Value.IsActive.Should().BeTrue();
        result.Value.TotalExecutions.Should().Be(0);
    }

    [Fact]
    public async Task CreateSimulatorRule_DuplicateSlug_ShouldReturnConflict()
    {
        // Arrange
        var useCase = new CreateSimulatorRuleUseCase(_db, _tenantContext, _dt);
        var command = new CreateSimulatorRuleCommand(
            Name: "Rule 1",
            Slug: "dup-slug"
        );

        var first = await useCase.ExecuteAsync(command);
        first.IsSuccess.Should().BeTrue();

        // Act
        var second = await useCase.ExecuteAsync(command);

        // Assert
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("SimulatorRule.SlugTaken");
    }

    [Fact]
    public async Task ProcessSimulatorRequest_FixedStatus_ShouldReturnConfiguredStatusAndHeaders()
    {
        // Arrange
        var createUseCase = new CreateSimulatorRuleUseCase(_db, _tenantContext, _dt);
        var createResult = await createUseCase.ExecuteAsync(new CreateSimulatorRuleCommand(
            Name: "Strict 500 Outage",
            Slug: "strict-500",
            Strategy: SimulatorStrategy.FixedStatus,
            TargetStatusCode: 500,
            ResponseBody: "{\"error\": \"Internal Server Error\"}"
        ));
        createResult.IsSuccess.Should().BeTrue();

        var processUseCase = new ProcessSimulatorRequestUseCase(_db, _dt);

        // Act
        var result = await processUseCase.ExecuteAsync(
            slug: "sim_strict-500",
            httpMethod: "POST",
            path: "/api/v1/simulator/receive/sim_strict-500",
            queryString: null,
            headersJson: "{\"X-Webhook-Event\": \"order.created\"}",
            body: "{\"order_id\": \"ord_123\"}",
            contentType: "application/json",
            contentLength: 22,
            clientIp: "192.168.1.100"
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StatusCode.Should().Be(500);
        result.Value.Body.Should().Contain("Internal Server Error");
        result.Value.InjectedFault.Should().Contain("Fixed HTTP 500");

        // Verify DB logging
        var executions = await _db.SimulatorExecutions.ToListAsync();
        executions.Should().HaveCount(1);
        executions[0].SimulatedStatusCode.Should().Be(500);
        executions[0].RuleId.Should().Be(createResult.Value.Id);
    }

    [Fact]
    public async Task ProcessSimulatorRequest_SequentialRetryPattern_ShouldFailNStepsThenSucceed()
    {
        // Arrange
        var createUseCase = new CreateSimulatorRuleUseCase(_db, _tenantContext, _dt);
        var createResult = await createUseCase.ExecuteAsync(new CreateSimulatorRuleCommand(
            Name: "Retry Test 2x 503 then 200",
            Slug: "retry-test",
            Strategy: SimulatorStrategy.SequentialRetryPattern,
            TargetStatusCode: 503,
            SuccessStatusCode: 200,
            FailureStepCount: 2
        ));
        createResult.IsSuccess.Should().BeTrue();

        var processUseCase = new ProcessSimulatorRequestUseCase(_db, _dt);

        // Step 1: Should Fail with 503
        var step1 = await processUseCase.ExecuteAsync("sim_retry-test", "POST", "/sim", null, "{}", "{}", "application/json", 2, "127.0.0.1");
        step1.IsSuccess.Should().BeTrue();
        step1.Value.StatusCode.Should().Be(503);
        step1.Value.InjectedFault.Should().Contain("Step 1/2");

        // Step 2: Should Fail with 503
        var step2 = await processUseCase.ExecuteAsync("sim_retry-test", "POST", "/sim", null, "{}", "{}", "application/json", 2, "127.0.0.1");
        step2.IsSuccess.Should().BeTrue();
        step2.Value.StatusCode.Should().Be(503);
        step2.Value.InjectedFault.Should().Contain("Step 2/2");

        // Step 3: Should Succeed with 200
        var step3 = await processUseCase.ExecuteAsync("sim_retry-test", "POST", "/sim", null, "{}", "{}", "application/json", 2, "127.0.0.1");
        step3.IsSuccess.Should().BeTrue();
        step3.Value.StatusCode.Should().Be(200);
        step3.Value.InjectedFault.Should().Contain("Recovered");

        // Verify Rule Execution Counters
        var rule = await _db.SimulatorRules.FirstAsync(r => r.Id == createResult.Value.Id);
        rule.TotalExecutions.Should().Be(3);
        rule.TotalFailures.Should().Be(2);
        rule.TotalSuccesses.Should().Be(1);

        // Test Step Reset
        var resetUseCase = new ResetSimulatorRuleStepsUseCase(_db);
        var resetResult = await resetUseCase.ExecuteAsync(rule.Id);
        resetResult.IsSuccess.Should().BeTrue();

        var ruleAfterReset = await _db.SimulatorRules.FirstAsync(r => r.Id == createResult.Value.Id);
        ruleAfterReset.CurrentStepCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessSimulatorRequest_FailureRate_Deterministic100Percent_ShouldAlwaysFail()
    {
        // Arrange
        var createUseCase = new CreateSimulatorRuleUseCase(_db, _tenantContext, _dt);
        var createResult = await createUseCase.ExecuteAsync(new CreateSimulatorRuleCommand(
            Name: "100% Flaky Rule",
            Slug: "flaky-100",
            Strategy: SimulatorStrategy.FailureRate,
            TargetStatusCode: 502,
            SuccessStatusCode: 200,
            FailureRatePercent: 100.0
        ));
        createResult.IsSuccess.Should().BeTrue();

        var processUseCase = new ProcessSimulatorRequestUseCase(_db, _dt);

        // Act
        var result = await processUseCase.ExecuteAsync("sim_flaky-100", "POST", "/sim", null, "{}", "{}", "application/json", 2, "127.0.0.1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StatusCode.Should().Be(502);
        result.Value.InjectedFault.Should().Contain("Failure Rate Fault");
    }

    [Fact]
    public async Task ProcessSimulatorRequest_TimeoutStrategy_ShouldReturn504()
    {
        // Arrange
        var createUseCase = new CreateSimulatorRuleUseCase(_db, _tenantContext, _dt);
        var createResult = await createUseCase.ExecuteAsync(new CreateSimulatorRuleCommand(
            Name: "Timeout Rule",
            Slug: "timeout-sim",
            Strategy: SimulatorStrategy.Timeout,
            TargetStatusCode: 504,
            DelayMs: 10
        ));
        createResult.IsSuccess.Should().BeTrue();

        var processUseCase = new ProcessSimulatorRequestUseCase(_db, _dt);

        // Act
        var result = await processUseCase.ExecuteAsync("sim_timeout-sim", "POST", "/sim", null, "{}", "{}", "application/json", 2, "127.0.0.1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StatusCode.Should().Be(504);
        result.Value.InjectedFault.Should().Contain("Simulated Timeout");
    }

    [Fact]
    public async Task ProcessSimulatorRequest_MalformedJsonStrategy_ShouldReturnCorruptedBody()
    {
        // Arrange
        var createUseCase = new CreateSimulatorRuleUseCase(_db, _tenantContext, _dt);
        var createResult = await createUseCase.ExecuteAsync(new CreateSimulatorRuleCommand(
            Name: "Malformed JSON Rule",
            Slug: "malformed-sim",
            Strategy: SimulatorStrategy.MalformedJson,
            TargetStatusCode: 200
        ));
        createResult.IsSuccess.Should().BeTrue();

        var processUseCase = new ProcessSimulatorRequestUseCase(_db, _dt);

        // Act
        var result = await processUseCase.ExecuteAsync("sim_malformed-sim", "POST", "/sim", null, "{}", "{}", "application/json", 2, "127.0.0.1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Body.Should().Contain("corrupted_unterminated");
        result.Value.InjectedFault.Should().Contain("Malformed");
    }

    [Fact]
    public async Task ExecuteAdHocSimulation_429Status_ShouldInjectRetryAfterHeaders()
    {
        // Arrange
        var useCase = new ExecuteAdHocSimulationUseCase(_db, _dt, _tenantContext);

        // Act
        var result = await useCase.ExecuteAsync(
            requestedStatusCode: 429,
            delayMsParam: 0,
            failureRateParam: null,
            retryAfterParam: 45,
            customBodyParam: null,
            httpMethod: "POST",
            path: "/api/v1/simulator/http/429",
            queryString: null,
            headersJson: "{}",
            body: "{}",
            contentType: "application/json",
            contentLength: 2,
            clientIp: "127.0.0.1"
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StatusCode.Should().Be(429);
        result.Value.Headers.Should().ContainKey("Retry-After");
        result.Value.Headers["Retry-After"].Should().Be("45");
        result.Value.Headers.Should().ContainKey("X-RateLimit-Remaining");
        result.Value.Headers["X-RateLimit-Remaining"].Should().Be("0");
    }

    [Fact]
    public async Task GetSimulatorStats_ShouldReturnAggregatedMetrics()
    {
        // Arrange
        var createUseCase = new CreateSimulatorRuleUseCase(_db, _tenantContext, _dt);
        await createUseCase.ExecuteAsync(new CreateSimulatorRuleCommand(
            Name: "Stats Rule",
            Slug: "stats-rule",
            Strategy: SimulatorStrategy.FixedStatus,
            TargetStatusCode: 500
        ));

        var processUseCase = new ProcessSimulatorRequestUseCase(_db, _dt);
        await processUseCase.ExecuteAsync("sim_stats-rule", "POST", "/sim", null, "{}", "{}", "application/json", 2, "127.0.0.1");

        var statsUseCase = new GetSimulatorStatsUseCase(_db);

        // Act
        var result = await statsUseCase.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalExecutions.Should().Be(1);
        result.Value.TotalFailures.Should().Be(1);
        result.Value.TotalSuccesses.Should().Be(0);
        result.Value.OverallFailureRatePercent.Should().Be(100.0);
        result.Value.ActiveRulesCount.Should().Be(1);
        result.Value.StatusDistribution.Should().ContainKey("500");
    }

    [Fact]
    public async Task ClearSimulatorExecutions_ShouldDeleteAllLogs()
    {
        // Arrange
        var createUseCase = new CreateSimulatorRuleUseCase(_db, _tenantContext, _dt);
        await createUseCase.ExecuteAsync(new CreateSimulatorRuleCommand(
            Name: "Clear Logs Rule",
            Slug: "clear-rule",
            Strategy: SimulatorStrategy.FixedStatus,
            TargetStatusCode: 200
        ));

        var processUseCase = new ProcessSimulatorRequestUseCase(_db, _dt);
        await processUseCase.ExecuteAsync("sim_clear-rule", "POST", "/sim", null, "{}", "{}", "application/json", 2, "127.0.0.1");

        var executionsBefore = await _db.SimulatorExecutions.CountAsync();
        executionsBefore.Should().Be(1);

        var clearUseCase = new ClearSimulatorExecutionsUseCase(_db, _tenantContext);

        // Act
        var result = await clearUseCase.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var executionsAfter = await _db.SimulatorExecutions.CountAsync();
        executionsAfter.Should().Be(0);
    }
}
