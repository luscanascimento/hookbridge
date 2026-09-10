using System.Diagnostics;
using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Endpoints;
using HookBridge.Application.ControlPlane.UseCases.Payloads;
using HookBridge.Domain.Entities;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Security;

namespace HookBridge.UnitTests.Performance;

public sealed class PerformanceProfilingUnitTests
{
    [Fact]
    public void WebhookSigner_HighThroughputSigningAndVerification_ShouldBePerformantAndAccurate()
    {
        // Arrange
        var signer = new WebhookSigner();
        var secret = "whsec_test_secret_key_performance_profiling_123456789";
        var payload = """{"event":"order.created","id":"ord_1001","amount":99.99,"status":"completed"}""";
        var timestamp = DateTimeOffset.UtcNow;
        const int iterations = 10_000;

        // Act - Benchmark signature generation throughput
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var header = signer.GenerateSignatureHeader(payload, secret, timestamp);
            header.Should().StartWith("t=");
        }
        sw.Stop();

        // Assert - 10,000 iterations should execute in under 2 seconds on modern hardware
        sw.ElapsedMilliseconds.Should().BeLessThan(2000, "10k HMAC generations should execute in sub-millisecond per-op speed");

        // Act - Verify signature throughput
        var validHeader = signer.GenerateSignatureHeader(payload, secret, timestamp);
        var verifySw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var result = signer.VerifySignature(payload, validHeader, secret, TimeSpan.FromMinutes(5), timestamp);
            result.IsSuccess.Should().BeTrue();
        }
        verifySw.Stop();

        // Assert - 10,000 verifications should execute in under 2 seconds
        verifySw.ElapsedMilliseconds.Should().BeLessThan(2000, "10k HMAC verifications should be sub-millisecond");
    }

    [Fact]
    public void SubscriptionPatternMatcher_HighThroughput_ShouldProcessPatternsInstantly()
    {
        // Arrange
        var patterns = new[]
        {
            "*",
            "order.*",
            "order.created",
            "billing.invoice.paid",
            "user.signup",
            "payment.*"
        };

        var events = new[]
        {
            "order.created",
            "order.updated",
            "order.cancelled",
            "billing.invoice.paid",
            "billing.invoice.failed",
            "payment.session.completed",
            "user.profile.changed"
        };

        const int iterations = 50_000;
        var matchCount = 0;

        // Act - High throughput pattern matching
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var p = patterns[i % patterns.Length];
            var e = events[i % events.Length];
            if (SubscriptionPatternMatcher.Matches(p, e))
            {
                matchCount++;
            }
        }
        sw.Stop();

        // Assert
        matchCount.Should().BeGreaterThan(0);
        sw.ElapsedMilliseconds.Should().BeLessThan(500, "50,000 pattern evaluations should take under 500ms");
    }

    [Fact]
    public void SubscriptionPatternMatcher_BoundaryPatternEvaluations_ShouldBeAccurate()
    {
        // Exact
        SubscriptionPatternMatcher.Matches("order.created", "order.created").Should().BeTrue();
        SubscriptionPatternMatcher.Matches("Order.Created", "order.created").Should().BeTrue();
        SubscriptionPatternMatcher.Matches("order.created", "order.updated").Should().BeFalse();

        // Universal Wildcard
        SubscriptionPatternMatcher.Matches("*", "any.event.type").Should().BeTrue();
        SubscriptionPatternMatcher.Matches(" * ", "any.event.type").Should().BeTrue();

        // Prefix Wildcard
        SubscriptionPatternMatcher.Matches("order.*", "order.created").Should().BeTrue();
        SubscriptionPatternMatcher.Matches("order.*", "order.item.added").Should().BeTrue();
        SubscriptionPatternMatcher.Matches("order.*", "order").Should().BeTrue();
        SubscriptionPatternMatcher.Matches("order.*", "order_refund").Should().BeFalse();
        SubscriptionPatternMatcher.Matches("order.*", "invoice.order.created").Should().BeFalse();

        // Empty / Null
        SubscriptionPatternMatcher.Matches("", "order.created").Should().BeFalse();
        SubscriptionPatternMatcher.Matches("order.*", "").Should().BeFalse();
        SubscriptionPatternMatcher.Matches(null!, "order.created").Should().BeFalse();
    }

    [Fact]
    public void PayloadAnalyzer_LargePayloadProcessing_ShouldBeFastAndMemoryEfficient()
    {
        // Arrange
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid(), "perf-test-tenant");
        var useCase = new AnalyzePayloadUseCase(tenantContext);

        var largeJson = $$"""
        {
            "batchId": "{{Guid.NewGuid()}}",
            "timestamp": "2026-09-10T12:00:00Z",
            "description": "Multi-tenant payload processing benchmark with UTF-8 chars: áéíóú 🚀",
            "metadata": {
                "environment": "production",
                "cluster": "us-east-1",
                "retries": 3,
                "isActive": true
            },
            "records": [
                { "id": 1, "sku": "SKU-AAA", "price": 49.90, "inStock": true, "tags": ["sale", "promo"] },
                { "id": 2, "sku": "SKU-BBB", "price": 120.00, "inStock": false, "tags": ["clearance"] },
                { "id": 3, "sku": "SKU-CCC", "price": 19.99, "inStock": true, "tags": ["essential", "quick-ship"] }
            ]
        }
        """;

        const int iterations = 1000;

        // Act
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var result = useCase.Execute(new AnalyzePayloadRequest(largeJson));
            result.IsSuccess.Should().BeTrue();
        }
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(2500, "1,000 complex payload analyzes should complete in under 2.5s");

        var sample = useCase.Execute(new AnalyzePayloadRequest(largeJson)).Value;
        sample.IsMultibyte.Should().BeTrue();
        sample.NonAsciiCharacterCount.Should().BeGreaterThan(0);
        sample.ArrayCount.Should().Be(4); // records array + 3 tags arrays
        sample.ObjectCount.Should().Be(5); // root + metadata + 3 items
    }

    [Fact]
    public void EndpointHealthMetrics_LatencyQuantiles_ShouldProcessLargeSampleRapidly()
    {
        // Arrange
        var deliveryId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var attempts = new List<Attempt>(10_000);
        var rng = new Random(42);

        for (var i = 0; i < 10_000; i++)
        {
            var elapsed = rng.Next(15, 1200);
            attempts.Add(Attempt.Create(deliveryId, tenantId, (i % 3) + 1, 200, "{}", "{}", "{}", "{}", elapsed, null, DateTimeOffset.UtcNow).Value);
        }

        // Act
        var sw = Stopwatch.StartNew();
        var quantiles = GetEndpointHealthMetricsUseCase.ComputeLatencyQuantiles(attempts);
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(100, "Quantile computation on 10,000 attempts must be fast");
        quantiles.MinMs.Should().BeGreaterThanOrEqualTo(15);
        quantiles.MaxMs.Should().BeLessThanOrEqualTo(1200);
        quantiles.P50Ms.Should().BeLessThan(quantiles.P90Ms);
        quantiles.P90Ms.Should().BeLessThanOrEqualTo(quantiles.P95Ms);
        quantiles.P95Ms.Should().BeLessThanOrEqualTo(quantiles.P99Ms);
        quantiles.AverageMs.Should().BeGreaterThan(0);
    }
}
