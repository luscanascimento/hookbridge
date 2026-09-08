using System.Diagnostics;
using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.Services;
using HookBridge.Application.ControlPlane.UseCases.Observability;
using HookBridge.Domain.Diagnostics;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class ObservabilityUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly DateTimeProvider _dt;
    private readonly ITelemetryBuffer _telemetryBuffer;
    private readonly IObservabilityOptions _observabilityOptions;
    private readonly Guid _tenantId;

    public ObservabilityUseCasesTests()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "test-obs-tenant");

        _db = new HookBridgeDbContext(options, _tenantContext);
        _dt = new DateTimeProvider();
        _telemetryBuffer = Substitute.For<ITelemetryBuffer>();
        _observabilityOptions = Substitute.For<IObservabilityOptions>();

        _observabilityOptions.OtlpEndpoint.Returns((string?)null);
        _observabilityOptions.EnvironmentName.Returns("Testing");
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GetObservabilitySummaryUseCase_ShouldReturnValidSummary()
    {
        // Arrange
        _telemetryBuffer.TotalRecordedCount.Returns(42);
        var useCase = new GetObservabilitySummaryUseCase(_db, _observabilityOptions, _dt);

        // Act
        var result = await useCase.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var summary = result.Value;
        summary.ServiceName.Should().Be("HookBridge");
        summary.RegisteredInstrumentsCount.Should().Be(16);
        summary.ProcessWorkingSetMb.Should().BeGreaterThan(0);
        summary.MetricCounters.Should().ContainKey("hookbridge.deliveries.total");
        summary.MetricCounters.Should().ContainKey("hookbridge.deliveries.succeeded");
        summary.MetricCounters.Should().ContainKey("hookbridge.deliveries.failed");
    }

    [Fact]
    public async Task GetMetricInstrumentsUseCase_ShouldReturnAll16RegisteredInstruments()
    {
        // Arrange
        var useCase = new GetMetricInstrumentsUseCase(_db);

        // Act
        var result = await useCase.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var list = result.Value;
        list.Should().HaveCount(16);

        var eventsPublished = list.Single(i => i.Name == "hookbridge.events.published");
        eventsPublished.Unit.Should().Be("{event}");
        eventsPublished.Type.Should().Be("Counter");

        var deliveryLatency = list.Single(i => i.Name == "hookbridge.delivery.latency");
        deliveryLatency.Unit.Should().Be("ms");
        deliveryLatency.Type.Should().Be("Histogram");

        var signalR = list.Single(i => i.Name == "hookbridge.signalr.active_connections");
        signalR.Type.Should().Be("UpDownCounter");
    }

    [Fact]
    public async Task GetRecentCapturedSpansUseCase_ShouldReturnSpansFromBuffer()
    {
        // Arrange
        var fakeSpans = new List<CapturedSpanDto>
        {
            new("trace-1", "span-1", null, "hookbridge.gateway.ingest", "HookBridge.ControlPlane", 12.5, DateTimeOffset.UtcNow, "Ok", new Dictionary<string, string>(), new Dictionary<string, string>()),
            new("trace-1", "span-2", "span-1", "hookbridge.webhook.signing", "HookBridge.ControlPlane", 3.2, DateTimeOffset.UtcNow, "Ok", new Dictionary<string, string>(), new Dictionary<string, string>())
        };

        _telemetryBuffer.GetRecentSpans(10).Returns(fakeSpans);
        var useCase = new GetRecentCapturedSpansUseCase(_telemetryBuffer);

        // Act
        var result = await useCase.ExecuteAsync(10, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].OperationName.Should().Be("hookbridge.gateway.ingest");
        result.Value[1].OperationName.Should().Be("hookbridge.webhook.signing");
    }

    [Fact]
    public async Task GenerateSyntheticTraceUseCase_ShouldGenerateFull4SpanWaterfall()
    {
        // Arrange
        var useCase = new GenerateSyntheticTraceUseCase(_tenantContext, _dt);
        var command = new SyntheticTraceCommand("invoice.paid", IncludeFailure: false, DelayMs: 5);

        // Act
        var result = await useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var trace = result.Value;
        trace.TraceId.Should().HaveLength(32);
        trace.TotalSpansGenerated.Should().Be(4);
        trace.Spans.Should().HaveCount(4);

        trace.Spans.Select(s => s.OperationName).Should().ContainInOrder(
            "hookbridge.gateway.ingest",
            "hookbridge.webhook.signing",
            "hookbridge.outbox.persist",
            "hookbridge.delivery.dispatch"
        );

        trace.Spans.All(s => s.Status == "Ok").Should().BeTrue();
        trace.Spans.All(s => s.TraceId == trace.TraceId).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateSyntheticTraceUseCase_WhenFailureInjected_ShouldRecordErrorSpan()
    {
        // Arrange
        var useCase = new GenerateSyntheticTraceUseCase(_tenantContext, _dt);
        var command = new SyntheticTraceCommand("payment.failed", IncludeFailure: true, DelayMs: 5);

        // Act
        var result = await useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var trace = result.Value;
        var dispatchSpan = trace.Spans.Single(s => s.OperationName == "hookbridge.delivery.dispatch");

        dispatchSpan.Status.Should().Be("Error");
        dispatchSpan.Tags.Should().ContainKey(HookBridgeDiagnostics.TagHttpStatusCode);
        dispatchSpan.Tags[HookBridgeDiagnostics.TagHttpStatusCode].Should().Be("500");
    }

    [Fact]
    public async Task GetPrometheusMetricsUseCase_ShouldFormatPrometheusTextProperly()
    {
        // Arrange
        var useCase = new GetPrometheusMetricsUseCase(_db);

        // Act
        var text = await useCase.ExecuteAsync(CancellationToken.None);

        // Assert
        text.Should().NotBeNullOrWhiteSpace();
        text.Should().Contain("# HELP process_working_set_bytes");
        text.Should().Contain("# TYPE process_working_set_bytes gauge");
        text.Should().Contain("# HELP hookbridge_events_published_total");
        text.Should().Contain("# TYPE hookbridge_events_published_total counter");
        text.Should().Contain("hookbridge_deliveries_dispatched_total");
    }
}
