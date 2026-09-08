using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Observability;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class ObservabilityEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ObservabilityEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync(string? slugPrefix = null)
    {
        var slug = $"{slugPrefix ?? "obs"}-{Guid.NewGuid():N}"[..16];
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Observability Org", $"{slug}@test.com", "Password#2026"));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
    }

    [Fact]
    public async Task GetPrometheusMetrics_ShouldReturnPublicPlaintextContent()
    {
        // Act
        var res = await _client.GetAsync("/metrics");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("# HELP process_working_set_bytes");
        body.Should().Contain("hookbridge_events_published_total");
    }

    [Fact]
    public async Task GetObservabilitySummary_ShouldReturnSummaryResponse()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("obssummary");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var res = await _client.GetAsync("/api/v1/observability/summary");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await res.Content.ReadFromJsonAsync<ObservabilitySummaryResponse>(JsonOptions);

        summary.Should().NotBeNull();
        summary!.ServiceName.Should().Be("HookBridge");
        summary.RegisteredInstrumentsCount.Should().Be(16);
        summary.ProcessWorkingSetMb.Should().BeGreaterThan(0);
        summary.MetricCounters.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMetricInstruments_ShouldReturnAll16Instruments()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("obsinstruments");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var res = await _client.GetAsync("/api/v1/observability/instruments");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var instruments = await res.Content.ReadFromJsonAsync<List<MetricInstrumentDto>>(JsonOptions);

        instruments.Should().NotBeNull();
        instruments.Should().HaveCount(16);
        instruments!.Any(i => i.Name == "hookbridge.events.published").Should().BeTrue();
        instruments.Any(i => i.Name == "hookbridge.deliveries.dispatched").Should().BeTrue();
    }

    [Fact]
    public async Task GetRecentSpans_ShouldReturnListOfCapturedSpans()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("obsspans");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var res = await _client.GetAsync("/api/v1/observability/spans?count=20");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var spans = await res.Content.ReadFromJsonAsync<List<CapturedSpanDto>>(JsonOptions);

        spans.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateSyntheticTrace_ShouldExecuteTraceAndReturnDAG()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("obstrace");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var command = new SyntheticTraceCommand("order.created", IncludeFailure: false, DelayMs: 10);

        // Act
        var res = await _client.PostAsJsonAsync("/api/v1/observability/synthetic-trace", command);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await res.Content.ReadFromJsonAsync<SyntheticTraceResponse>(JsonOptions);

        response.Should().NotBeNull();
        response!.TraceId.Should().HaveLength(32);
        response.TotalSpansGenerated.Should().Be(4);
        response.Spans.Should().HaveCount(4);
    }

    [Fact]
    public async Task TraceContextEnricherMiddleware_ShouldPropagateW3CTraceParentAndSetResponseHeaders()
    {
        // Arrange
        var traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        var spanId = "00f067aa0ba902b7";
        var traceParent = $"00-{traceId}-{spanId}-01";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/metrics");
        request.Headers.Add("traceparent", traceParent);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().Contain(h => h.Key == "X-Trace-Id");
        response.Headers.GetValues("X-Trace-Id").First().Should().Be(traceId);
        response.Headers.Should().Contain(h => h.Key == "X-Correlation-Id");
    }
}
