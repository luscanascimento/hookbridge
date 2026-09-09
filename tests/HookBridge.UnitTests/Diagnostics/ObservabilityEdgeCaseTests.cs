using System.Diagnostics;
using System.Globalization;
using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.UseCases.Observability;
using HookBridge.Domain.Diagnostics;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.UnitTests.Diagnostics;

public sealed class ObservabilityEdgeCaseTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly DateTimeProvider _dt;
    private readonly Guid _tenantId;

    public ObservabilityEdgeCaseTests()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "telemetry-tenant");

        _db = new HookBridgeDbContext(options, _tenantContext);
        _dt = new DateTimeProvider();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void InMemoryTelemetryBuffer_WhenExceedingMaxBufferSize_EvictsOldestSpans()
    {
        var buffer = new InMemoryTelemetryBuffer();
        var source = new ActivitySource("HookBridge.Testing.Buffer");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        // Enqueue 250 activities into buffer (MaxBufferSize is 200)
        for (int i = 1; i <= 250; i++)
        {
            using var activity = source.StartActivity($"span-{i}");
            if (activity != null)
            {
                activity.SetTag("index", i.ToString(CultureInfo.InvariantCulture));
                activity.Stop();
                buffer.OnEnd(activity);
            }
        }

        buffer.TotalRecordedCount.Should().Be(200);

        var recentSpans = buffer.GetRecentSpans(200);
        recentSpans.Should().HaveCount(200);

        // The newest span should be index 250
        recentSpans[0].Tags["index"].Should().Be("250");
        // The oldest remaining span should be index 51
        recentSpans[^1].Tags["index"].Should().Be("51");

        buffer.Clear();
        buffer.TotalRecordedCount.Should().Be(0);
    }

    [Fact]
    public void InMemoryTelemetryBuffer_ConcurrentSpanIngestion_IsThreadSafe()
    {
        var buffer = new InMemoryTelemetryBuffer();
        var source = new ActivitySource("HookBridge.Testing.Concurrent");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        Parallel.For(0, 100, i =>
        {
            using var activity = source.StartActivity($"concurrent-span-{i}");
            if (activity != null)
            {
                activity.SetTag("thread", Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture));
                activity.Stop();
                buffer.OnEnd(activity);
            }
        });

        buffer.TotalRecordedCount.Should().Be(100);
        var spans = buffer.GetRecentSpans(50);
        spans.Should().HaveCount(50);
    }

    [Fact]
    public async Task SyntheticTracePipeline_Generates_Complete_Span_DAG_WithCustomStatus()
    {
        var useCase = new GenerateSyntheticTraceUseCase(_tenantContext, _dt);
        var command = new SyntheticTraceCommand("invoice.paid", IncludeFailure: true, DelayMs: 5);

        var result = await useCase.ExecuteAsync(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        var trace = result.Value;

        trace.TraceId.Should().NotBeNullOrWhiteSpace();
        trace.Spans.Should().HaveCount(4);

        var dispatchSpan = trace.Spans.Single(s => s.OperationName == "hookbridge.delivery.dispatch");
        dispatchSpan.Status.Should().Be("Error");
        dispatchSpan.Tags.Should().ContainKey(HookBridgeDiagnostics.TagHttpStatusCode);
        dispatchSpan.Tags[HookBridgeDiagnostics.TagHttpStatusCode].Should().Be("500");
    }

    [Fact]
    public async Task PrometheusMetricsFormatter_Formats_ClrAndDomainMetrics_WithoutSpecialCharErrors()
    {
        var useCase = new GetPrometheusMetricsUseCase(_db);
        var output = await useCase.ExecuteAsync(CancellationToken.None);

        output.Should().NotBeNullOrWhiteSpace();
        output.Should().Contain("# HELP process_working_set_bytes");
        output.Should().Contain("# TYPE process_working_set_bytes gauge");
        output.Should().Contain("# HELP hookbridge_events_published_total");
        output.Should().Contain("hookbridge_deliveries_dispatched_total");
    }
}
