using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.UseCases.Endpoints;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class EndpointHealthUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly DateTimeProvider _dt;
    private readonly Guid _tenantId;

    public EndpointHealthUseCasesTests()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "health-test-tenant");

        _db = new HookBridgeDbContext(options, _tenantContext);
        _dt = new DateTimeProvider();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GetEndpointHealthMetrics_WithDeliveriesAndAttempts_ShouldComputeQuantilesAndHealthScore()
    {
        // Arrange
        var now = _dt.UtcNow;
        var appId = Guid.NewGuid();
        var endpoint = Endpoint.Create(_tenantId, appId, "https://api.example.com/webhooks", "Test Endpoint", now).Value;
        _db.Endpoints.Add(endpoint);

        var subId = Guid.NewGuid();

        // Create 5 deliveries with various latencies
        var latencies = new[] { 100L, 200L, 300L, 800L, 1200L };
        for (var i = 0; i < latencies.Length; i++)
        {
            var del = Delivery.Create(_tenantId, Guid.NewGuid(), endpoint.Id, subId, "order.created", $"corr_{i}", null, now.AddHours(-i)).Value;
            del.MarkDispatched(now.AddHours(-i));
            del.MarkSuccess(now.AddHours(-i));

            var att = Attempt.Create(
                del.Id,
                _tenantId,
                1,
                200,
                "{}",
                "{}",
                "{}",
                "{}",
                latencies[i],
                null,
                now.AddHours(-i)).Value;

            del.Attempts.Add(att);
            _db.Deliveries.Add(del);
        }

        await _db.SaveChangesAsync();

        var useCase = new GetEndpointHealthMetricsUseCase(_db, _tenantContext, _dt);

        // Act
        var result = await useCase.ExecuteAsync(endpoint.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var health = result.Value;
        health.EndpointId.Should().Be(endpoint.Id);
        health.Status.Should().Be("Active");
        health.CircuitState.Should().Be("Closed");
        health.TotalDeliveries.Should().Be(5);
        health.SuccessCount.Should().Be(5);
        health.FailedCount.Should().Be(0);
        health.UptimePercent.Should().Be(100.0);
        health.HealthScorePercent.Should().BeGreaterThanOrEqualTo(90);

        // Quantiles verification
        health.Latencies.MinMs.Should().Be(100);
        health.Latencies.MaxMs.Should().Be(1200);
        health.Latencies.P50Ms.Should().Be(300);
        health.Latencies.AverageMs.Should().Be(520);
        health.HourlyBuckets.Should().NotBeEmpty();
        health.Incidents.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEndpointHealthMetrics_WithConsecutiveFailures_ShouldTripCircuitBreakerAndAlert()
    {
        // Arrange
        var now = _dt.UtcNow;
        var appId = Guid.NewGuid();
        var endpoint = Endpoint.Create(_tenantId, appId, "https://api.flaky-server.com/hooks", "Flaky Destination", now).Value;
        _db.Endpoints.Add(endpoint);

        var subId = Guid.NewGuid();

        // Create 5 consecutive failed deliveries
        for (var i = 0; i < 5; i++)
        {
            var del = Delivery.Create(_tenantId, Guid.NewGuid(), endpoint.Id, subId, "invoice.payment_failed", $"corr_fail_{i}", null, now.AddMinutes(-i * 10)).Value;
            del.MarkDispatched(now.AddMinutes(-i * 10));
            del.MarkFailed(now.AddMinutes(-i * 10));

            var att = Attempt.Create(
                del.Id,
                _tenantId,
                1,
                503,
                "{}",
                "{}",
                "{}",
                "Service Unavailable",
                3200,
                "HTTP 503 Service Unavailable",
                now.AddMinutes(-i * 10)).Value;

            del.Attempts.Add(att);
            _db.Deliveries.Add(del);
        }

        await _db.SaveChangesAsync();

        var useCase = new GetEndpointHealthMetricsUseCase(_db, _tenantContext, _dt);

        // Act
        var result = await useCase.ExecuteAsync(endpoint.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var health = result.Value;
        health.CircuitState.Should().Be("Open");
        health.ConsecutiveFailures.Should().Be(5);
        health.ErrorRatePercent.Should().Be(100.0);
        health.HealthScorePercent.Should().BeLessThan(40);
        health.Incidents.Should().Contain(i => i.Type == "CircuitBroken" && i.Severity == "Critical");
        health.Incidents.Should().Contain(i => i.Type == "HighErrorRate");
    }

    [Fact]
    public async Task GetTenantEndpointsHealthSummary_ShouldAggregateAcrossEndpoints()
    {
        // Arrange
        var now = _dt.UtcNow;
        var appId = Guid.NewGuid();

        var ep1 = Endpoint.Create(_tenantId, appId, "https://ep1.com", "Healthy", now).Value;
        var ep2 = Endpoint.Create(_tenantId, appId, "https://ep2.com", "Degraded", now).Value;
        _db.Endpoints.AddRange(ep1, ep2);

        // ep1 has 1 success
        var del1 = Delivery.Create(_tenantId, Guid.NewGuid(), ep1.Id, Guid.NewGuid(), "evt.1", "c1", null, now).Value;
        del1.MarkSuccess(now);
        del1.Attempts.Add(Attempt.Create(del1.Id, _tenantId, 1, 200, "{}", "{}", null, null, 150, null, now).Value);

        // ep2 has 1 failure
        var del2 = Delivery.Create(_tenantId, Guid.NewGuid(), ep2.Id, Guid.NewGuid(), "evt.2", "c2", null, now).Value;
        del2.MarkFailed(now);
        del2.Attempts.Add(Attempt.Create(del2.Id, _tenantId, 1, 500, "{}", "{}", null, null, 4000, "Internal Server Error", now).Value);

        _db.Deliveries.AddRange(del1, del2);
        await _db.SaveChangesAsync();

        var useCase = new GetTenantEndpointsHealthSummaryUseCase(_db, _tenantContext, _dt);

        // Act
        var result = await useCase.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var summary = result.Value;
        summary.TotalEndpoints.Should().Be(2);
        summary.Endpoints.Should().HaveCount(2);
        summary.Endpoints.Should().Contain(e => e.EndpointId == ep1.Id && e.HealthScorePercent >= 90);
        summary.Endpoints.Should().Contain(e => e.EndpointId == ep2.Id && e.HealthScorePercent < 90);
    }
}
