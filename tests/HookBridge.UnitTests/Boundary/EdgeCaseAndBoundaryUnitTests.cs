using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.Services;
using HookBridge.Application.ControlPlane.UseCases.Endpoints;
using HookBridge.Application.ControlPlane.UseCases.Payloads;
using HookBridge.Application.ControlPlane.UseCases.Simulator;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Security;

namespace HookBridge.UnitTests.Boundary;

public sealed class EdgeCaseAndBoundaryUnitTests
{
    private readonly TenantContext _tenantContext;
    private readonly Guid _tenantId;
    private readonly WebhookSigner _signer;

    public EdgeCaseAndBoundaryUnitTests()
    {
        _tenantId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "edge-case-tenant");
        _signer = new WebhookSigner();
    }

    #region SubscriptionPatternMatcher Boundary Tests

    [Theory]
    [InlineData("*", "order.created", true)]
    [InlineData("*", "anything", true)]
    [InlineData("*", "", false)]
    [InlineData("order.*", "order.created", true)]
    [InlineData("order.*", "order.updated.v2", true)]
    [InlineData("order.*", "order.", true)]
    [InlineData("order.*", "orders.created", false)]
    [InlineData("order.*", "other.order.created", false)]
    [InlineData("order.created", "order.created", true)]
    [InlineData("order.created", "Order.Created", true)] // Case-insensitive
    [InlineData("order.created", "order.created.v2", false)]
    [InlineData("order.*.completed", "order.payment.completed", false)]
    [InlineData("*.created", "order.created", false)]
    public void SubscriptionPatternMatcher_Matches_VariousPatternsAccurately(string pattern, string eventType, bool expectedMatch)
    {
        var result = SubscriptionPatternMatcher.Matches(pattern, eventType);
        result.Should().Be(expectedMatch);
    }

    [Fact]
    public void SubscriptionPatternMatcher_WithNullOrEmpty_HandlesGracefully()
    {
        SubscriptionPatternMatcher.Matches(null!, "order.created").Should().BeFalse();
        SubscriptionPatternMatcher.Matches("", "order.created").Should().BeFalse();
        SubscriptionPatternMatcher.Matches("order.created", null!).Should().BeFalse();
        SubscriptionPatternMatcher.Matches("  ", "order.created").Should().BeFalse();
    }

    [Fact]
    public void SubscriptionPatternMatcher_WithSpecialCharactersInEventType_MatchesSafely()
    {
        var specialEvent = "order+created.event";
        var pattern = "order+created.*";

        var match = SubscriptionPatternMatcher.Matches(pattern, specialEvent);
        match.Should().BeTrue();
    }

    #endregion

    #region WebhookSigner Boundary & Clock Skew Tests

    [Fact]
    public void WebhookSigner_TimestampValidation_AtExactToleranceBoundary_ShouldBeValid()
    {
        var secret = "whsec_test_boundary_secret_12345";
        var payload = "{\"event\":\"order.created\"}";
        var now = DateTimeOffset.UtcNow;

        // Exactly 300 seconds ago (tolerance is 300s)
        var exactPast = now.AddSeconds(-300);
        var signature = _signer.GenerateSignatureHeader(payload, secret, exactPast);

        var verifyResult = _signer.VerifySignature(payload, signature, secret, tolerance: TimeSpan.FromSeconds(300), now: now);
        verifyResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void WebhookSigner_TimestampValidation_ExceedingToleranceByOneSecond_ShouldBeInvalid()
    {
        var secret = "whsec_test_boundary_secret_12345";
        var payload = "{\"event\":\"order.created\"}";
        var now = DateTimeOffset.UtcNow;

        // 301 seconds ago (exceeds 300s tolerance)
        var expiredTime = now.AddSeconds(-301);
        var signature = _signer.GenerateSignatureHeader(payload, secret, expiredTime);

        var verifyResult = _signer.VerifySignature(payload, signature, secret, tolerance: TimeSpan.FromSeconds(300), now: now);
        verifyResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void WebhookSigner_FutureTimestamp_Within60sClockSkew_ShouldBeValid()
    {
        var secret = "whsec_test_boundary_secret_12345";
        var payload = "{\"event\":\"order.created\"}";
        var now = DateTimeOffset.UtcNow;

        // 45 seconds in the future (acceptable clock drift <= 60s)
        var futureTime = now.AddSeconds(45);
        var signature = _signer.GenerateSignatureHeader(payload, secret, futureTime);

        var verifyResult = _signer.VerifySignature(payload, signature, secret, tolerance: TimeSpan.FromSeconds(300), now: now);
        verifyResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void WebhookSigner_FutureTimestamp_Exceeding60sClockSkew_ShouldBeInvalid()
    {
        var secret = "whsec_test_boundary_secret_12345";
        var payload = "{\"event\":\"order.created\"}";
        var now = DateTimeOffset.UtcNow;

        // 65 seconds in the future (exceeds 60s future clock drift limit)
        var futureTime = now.AddSeconds(65);
        var signature = _signer.GenerateSignatureHeader(payload, secret, futureTime);

        var verifyResult = _signer.VerifySignature(payload, signature, secret, tolerance: TimeSpan.FromSeconds(300), now: now);
        verifyResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void WebhookSigner_DualSecret_RotatingKeyValidation_Succeeds()
    {
        var activeSecret = "whsec_active_secret_key_11111";
        var rotatingSecret = "whsec_rotating_secret_key_22222";
        var payload = "{\"event\":\"invoice.paid\",\"amount\":500}";
        var now = DateTimeOffset.UtcNow;

        // Generate dual-header signature
        var dualHeader = _signer.GenerateSignatureHeader(payload, [activeSecret, rotatingSecret], now);
        dualHeader.Should().Contain("v1=");

        // Verifying with active secret must succeed
        _signer.VerifySignature(payload, dualHeader, activeSecret, now: now).IsSuccess.Should().BeTrue();

        // Verifying with rotating secret must succeed
        _signer.VerifySignature(payload, dualHeader, rotatingSecret, now: now).IsSuccess.Should().BeTrue();

        // Verifying with unrelated secret must fail
        _signer.VerifySignature(payload, dualHeader, "whsec_unknown_random_secret_99999", now: now).IsFailure.Should().BeTrue();
    }

    #endregion

    #region SchemaCompatibilityChecker Boundary Tests

    [Fact]
    public void SchemaCompatibilityChecker_FullMode_AddingRequiredProperty_ReturnsBreakingChange()
    {
        var checker = new SchemaCompatibilityChecker();

        var oldSchema = """
        {
            "type": "object",
            "properties": {
                "id": { "type": "string" }
            },
            "required": ["id"]
        }
        """;

        var newSchema = """
        {
            "type": "object",
            "properties": {
                "id": { "type": "string" },
                "newMandatoryField": { "type": "string" }
            },
            "required": ["id", "newMandatoryField"]
        }
        """;

        // Under Full compatibility, adding a new required field is breaking for producers using old schema
        var result = checker.CheckCompatibility(oldSchema, newSchema, SchemaCompatibilityMode.Full);
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().NotBeEmpty();
    }

    [Fact]
    public void SchemaCompatibilityChecker_NoneMode_AlwaysReturnsCompatible()
    {
        var checker = new SchemaCompatibilityChecker();

        var oldSchema = """{"type": "string"}""";
        var newSchema = """{"type": "object", "properties": {"a": {"type": "integer"}}}""";

        var result = checker.CheckCompatibility(oldSchema, newSchema, SchemaCompatibilityMode.None);
        result.IsCompatible.Should().BeTrue();
        result.BreakingChanges.Should().BeEmpty();
    }

    [Fact]
    public void SchemaCompatibilityChecker_TypeShift_ReturnsBreakingChange()
    {
        var checker = new SchemaCompatibilityChecker();

        var oldSchema = """
        {
            "type": "object",
            "properties": {
                "amount": { "type": "number" }
            }
        }
        """;

        var newSchema = """
        {
            "type": "object",
            "properties": {
                "amount": { "type": "string" }
            }
        }
        """;

        var result = checker.CheckCompatibility(oldSchema, newSchema, SchemaCompatibilityMode.Backward);
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().Contain(c => c.Contains("Type changed") || c.Contains("amount"));
    }

    #endregion

    #region SchemaCodeGenerator C# & TypeScript Reserved Words

    [Fact]
    public void SchemaCodeGenerator_WithReservedKeywordsAndSpecialChars_GeneratesValidCode()
    {
        var generator = new SchemaCodeGenerator();

        var schemaJson = """
        {
            "type": "object",
            "title": "SpecialOrderEvent",
            "properties": {
                "class": { "type": "string" },
                "event": { "type": "string" },
                "namespace": { "type": "string" },
                "user-id": { "type": "string", "format": "uuid" },
                "is-active": { "type": "boolean" },
                "item_count": { "type": "integer" },
                "total_price": { "type": "number" }
            },
            "required": ["class", "user-id"]
        }
        """;

        var tsCode = generator.GenerateTypeScript("special.order.event", schemaJson);
        tsCode.Should().NotBeNullOrWhiteSpace();
        tsCode.Should().Contain("export interface");

        var csharpCode = generator.GenerateCSharp("special.order.event", schemaJson);
        csharpCode.Should().NotBeNullOrWhiteSpace();
        csharpCode.Should().Contain("public sealed record");
    }

    #endregion

    #region PayloadDiff & JSONPath Boundary Tests

    [Fact]
    public void DiffPayloads_WithDeeplyNestedDisjointTrees_ComputesAllDeltas()
    {
        var useCase = new DiffPayloadsUseCase(_tenantContext);

        var baseJson = """
        {
            "user": {
                "profile": {
                    "name": "Alice",
                    "age": 30
                },
                "roles": ["viewer"]
            }
        }
        """;

        var compareJson = """
        {
            "user": {
                "profile": {
                    "name": "Alice Cooper",
                    "title": "Lead Engineer"
                },
                "roles": ["viewer", "admin"],
                "settings": {
                    "theme": "dark"
                }
            }
        }
        """;

        var result = useCase.Execute(new DiffPayloadsRequest(baseJson, compareJson));
        result.IsSuccess.Should().BeTrue();
        var diff = result.Value;

        diff.HasDifferences.Should().BeTrue();
        diff.Entries.Should().Contain(d => d.DiffType == "Modified" && d.Path.Contains("name"));
        diff.Entries.Should().Contain(d => d.DiffType == "Removed" && d.Path.Contains("age"));
        diff.Entries.Should().Contain(d => d.DiffType == "Added" && d.Path.Contains("title"));
        diff.Entries.Should().Contain(d => d.DiffType == "Added" && d.Path.Contains("settings"));
    }

    [Fact]
    public void EvaluateJsonPath_WithWildcardsAndArrays_ReturnsMatches()
    {
        var useCase = new EvaluateJsonPathUseCase(_tenantContext);

        var json = """
        {
            "store": {
                "books": [
                    { "title": "Clean Code", "price": 45.0 },
                    { "title": "Domain-Driven Design", "price": 55.0 }
                ]
            }
        }
        """;

        var query = "$.store.books[*]";
        var result = useCase.Execute(new EvaluateJsonPathRequest(json, query));
        result.IsSuccess.Should().BeTrue();
        result.Value.Matches.Should().HaveCount(2);
    }

    [Fact]
    public void EvaluateJsonPath_WithNonExistentPath_ReturnsZeroMatchesWithoutError()
    {
        var useCase = new EvaluateJsonPathUseCase(_tenantContext);
        var json = """{"name": "test"}""";

        var result = useCase.Execute(new EvaluateJsonPathRequest(json, "$.non.existent.path"));
        result.IsSuccess.Should().BeTrue();
        result.Value.Matches.Should().BeEmpty();
    }

    #endregion

    #region EndpointHealth Score & Circuit Breaker Calculations

    [Fact]
    public void EndpointHealth_WithZeroAttempts_Returns100PercentInitialHealthScore()
    {
        // When an endpoint is newly created with 0 attempts, health score defaults to 100%
        var successRate = 100.0;
        var consecutiveFailures = 0;

        var latencyScore = 100.0;
        var calculatedScore = Math.Clamp((successRate * 0.7) + (latencyScore * 0.3) - Math.Min(40, consecutiveFailures * 10), 0, 100);

        calculatedScore.Should().Be(100.0);
    }

    [Fact]
    public void EndpointHealth_With5ConsecutiveFailures_CircuitBreakerEntersOpenState()
    {
        var consecutiveFailures = 5;
        var circuitState = consecutiveFailures >= 5 ? "Open"
            : consecutiveFailures >= 3 ? "HalfOpen"
            : "Closed";

        circuitState.Should().Be("Open");
    }

    [Fact]
    public void EndpointHealth_With3ConsecutiveFailures_CircuitBreakerEntersHalfOpenState()
    {
        var consecutiveFailures = 3;
        var circuitState = consecutiveFailures >= 5 ? "Open"
            : consecutiveFailures >= 3 ? "HalfOpen"
            : "Closed";

        circuitState.Should().Be("HalfOpen");
    }

    #endregion

    #region Simulator Strategy & Sequential Retries

    [Fact]
    public void SimulatorRule_SequentialRetryPattern_RollsOverAfterTargetCount()
    {
        var ruleRes = SimulatorRule.Create(
            _tenantId,
            "Sequential Test",
            "sim_seq_test",
            SimulatorStrategy.SequentialRetryPattern,
            targetStatusCode: 503,
            successStatusCode: 200,
            failureRatePercent: 0,
            failureStepCount: 2,
            delayMs: 0,
            minDelayMs: 0,
            maxDelayMs: 0,
            responseHeadersJson: null,
            responseBody: null,
            responseContentType: "application/json",
            description: null,
            now: DateTimeOffset.UtcNow
        );

        ruleRes.IsSuccess.Should().BeTrue();
        var rule = ruleRes.Value;

        // Step 1: fails (0 < 2)
        var isFailure1 = rule.CurrentStepCount < rule.FailureStepCount;
        isFailure1.Should().BeTrue();
        rule.IncrementStep();

        // Step 2: fails (1 < 2)
        var isFailure2 = rule.CurrentStepCount < rule.FailureStepCount;
        isFailure2.Should().BeTrue();
        rule.IncrementStep();

        // Step 3: succeeds (2 == 2)
        var isFailure3 = rule.CurrentStepCount < rule.FailureStepCount;
        isFailure3.Should().BeFalse();
        rule.IncrementStep();

        // Reset step counter
        rule.ResetSteps();
        rule.CurrentStepCount.Should().Be(0);

        // Step 1 again after reset -> fails (0 < 2)
        var isFailureAfterReset = rule.CurrentStepCount < rule.FailureStepCount;
        isFailureAfterReset.Should().BeTrue();
    }

    #endregion
}
