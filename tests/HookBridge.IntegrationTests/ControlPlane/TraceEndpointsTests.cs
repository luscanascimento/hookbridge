using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class TraceEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] ExpectedSpanNames =
    [
        "hookbridge.gateway.ingest",
        "eventflow.transactional_outbox",
        "rabbitmq.broker_publish",
        "eventflow.consumer_worker"
    ];

    public TraceEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TraceExplorer_Query_And_Waterfall_Correlation_Lifecycle()
    {
        // Arrange
        var slug = $"trc-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Trace Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Trace App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.github.com/webhook", "Webhook Endpoint", 600, 15, new List<string> { "order.*" }));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // 1. Publish Event with Correlation ID
        var correlationId = $"corr_trace_{Guid.NewGuid():N}";
        var payload = JsonDocument.Parse("{\"orderId\":999,\"amount\":150.00}").RootElement;
        var pubRes = await _client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand(
            "order.created",
            payload,
            CorrelationId: correlationId));
        pubRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var pub = await pubRes.Content.ReadFromJsonAsync<PublishEventResponse>(JsonOptions);
        pub!.DeliveriesScheduled.Should().Be(1);

        // 2. Fetch Delivery and record an attempt
        var deliveriesRes = await _client.GetAsync($"/api/v1/deliveries?correlationId={correlationId}");
        deliveriesRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var deliveriesPaged = await deliveriesRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        deliveriesPaged!.Items.Should().ContainSingle();
        var deliveryId = deliveriesPaged.Items[0].Id;

        var attemptRes = await _client.PostAsJsonAsync($"/api/v1/deliveries/{deliveryId}/attempts", new RecordDeliveryAttemptCommand(
            200, "{\"X-HookBridge-Signature\":\"sig_test\"}", "{\"amount\":150.00}", "{\"Content-Type\":\"application/json\"}", "{\"status\":\"ok\"}", 45, null, DeliveryStatus.Success));
        attemptRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. Query Traces List
        var tracesRes = await _client.GetAsync($"/api/v1/traces?query={correlationId}");
        tracesRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var tracesPaged = await tracesRes.Content.ReadFromJsonAsync<PagedList<TraceSummaryResponse>>(JsonOptions);
        tracesPaged.Should().NotBeNull();
        tracesPaged!.Items.Should().ContainSingle();

        var traceSummary = tracesPaged.Items[0];
        traceSummary.CorrelationId.Should().Be(correlationId);
        traceSummary.EventType.Should().Be("order.created");
        traceSummary.Status.Should().Be("Success");
        traceSummary.DeliveryCount.Should().Be(1);
        traceSummary.AttemptCount.Should().Be(1);
        traceSummary.SpanCount.Should().BeGreaterThanOrEqualTo(5);

        // 4. Get Trace Detail (Waterfall DAG)
        var detailRes = await _client.GetAsync($"/api/v1/traces/{correlationId}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailRes.Content.ReadFromJsonAsync<TraceDetailResponse>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.CorrelationId.Should().Be(correlationId);
        detail.OverallStatus.Should().Be("Success");
        detail.Deliveries.Should().ContainSingle();
        detail.Deliveries[0].Id.Should().Be(deliveryId);
        detail.Deliveries[0].Attempts.Should().ContainSingle();
        detail.Deliveries[0].Attempts[0].ElapsedMs.Should().Be(45);

        // Verify Spans in the Waterfall DAG
        detail.Spans.Should().NotBeEmpty();
        detail.Spans.Select(s => s.Name).Should().Contain(ExpectedSpanNames);

        var httpSpan = detail.Spans.FirstOrDefault(s => s.Name.StartsWith("http.post", StringComparison.Ordinal));
        httpSpan.Should().NotBeNull();
        httpSpan!.Status.Should().Be("Ok");
        httpSpan.DurationMs.Should().Be(45);
        httpSpan.Attributes.Should().ContainKey("http.status_code");
        httpSpan.Attributes["http.status_code"].Should().Be("200");
    }

    [Fact]
    public async Task TraceExplorer_Tenant_Isolation_Guarantees()
    {
        // Tenant 1 setup
        var slug1 = $"t1-{Guid.NewGuid():N}"[..16];
        var reg1 = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug1, "Tenant 1", $"{slug1}@test.com", "Password#2026"));
        var auth1 = await reg1.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth1!.AccessToken);

        var appRes1 = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("T1 App", null));
        var app1 = await appRes1.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes1 = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app1!.Id, "https://api.github.com/webhook", "T1 EP", 600, 15, new List<string> { "item.*" }));
        var ep1 = await epRes1.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        var correlation1 = $"corr_t1_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand(
            "item.updated",
            JsonDocument.Parse("{\"item\":\"1\"}").RootElement,
            CorrelationId: correlation1));

        // Tenant 2 setup
        var slug2 = $"t2-{Guid.NewGuid():N}"[..16];
        var reg2 = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug2, "Tenant 2", $"{slug2}@test.com", "Password#2026"));
        var auth2 = await reg2.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth2!.AccessToken);

        // Tenant 2 attempts to query Tenant 1's trace
        var t2ListRes = await _client.GetAsync($"/api/v1/traces?query={correlation1}");
        t2ListRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var t2Paged = await t2ListRes.Content.ReadFromJsonAsync<PagedList<TraceSummaryResponse>>(JsonOptions);
        t2Paged!.Items.Should().BeEmpty();

        var t2DetailRes = await _client.GetAsync($"/api/v1/traces/{correlation1}");
        t2DetailRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
