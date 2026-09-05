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

public class DeliveryEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DeliveryEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Delivery_Query_Details_Attempts_Stats_Lifecycle()
    {
        // Arrange
        var slug = $"del-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Delivery Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Del App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.github.com/webhook", "Webhooks", 600, 15, new List<string> { "invoice.*" }));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // 1. Publish Event to create a Delivery
        var payload = JsonDocument.Parse("{\"invoiceId\":\"inv_123\",\"total\":450.00}").RootElement;
        var pubRes = await _client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("invoice.generated", payload));
        pubRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var pub = await pubRes.Content.ReadFromJsonAsync<PublishEventResponse>(JsonOptions);
        pub!.DeliveriesScheduled.Should().Be(1);

        // 2. Query Deliveries list
        var listRes = await _client.GetAsync("/api/v1/deliveries");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedDeliveries = await listRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        pagedDeliveries.Should().NotBeNull();
        pagedDeliveries!.Items.Should().ContainSingle();
        pagedDeliveries.Items[0].EndpointUrl.Should().Be("https://api.github.com/webhook");

        var deliveryId = pagedDeliveries.Items[0].Id;

        // 3. Record an Attempt for this Delivery (Success with 25ms latency)
        var attemptRes = await _client.PostAsJsonAsync($"/api/v1/deliveries/{deliveryId}/attempts", new RecordDeliveryAttemptCommand(
            200, "{\"X-HookBridge-Delivery\":\"test\"}", "{\"total\":450.00}", "{\"Content-Type\":\"application/json\"}", "{\"status\":\"ok\"}", 25, null, DeliveryStatus.Success));
        attemptRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 4. Get Delivery Details (verify status updated to Success & attempt included)
        var detailRes = await _client.GetAsync($"/api/v1/deliveries/{deliveryId}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailRes.Content.ReadFromJsonAsync<DeliveryDetailResponse>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(DeliveryStatus.Success);
        detail.EndpointUrl.Should().Be("https://api.github.com/webhook");
        detail.DeliveredAt.Should().NotBeNull();
        detail.Attempts.Should().ContainSingle();
        detail.Attempts[0].HttpStatusCode.Should().Be(200);
        detail.Attempts[0].ElapsedMs.Should().Be(25);

        // 5. Query Delivery Stats
        var statsRes = await _client.GetAsync("/api/v1/deliveries/stats");
        statsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await statsRes.Content.ReadFromJsonAsync<DeliveryStatsResponse>(JsonOptions);
        stats.Should().NotBeNull();
        stats!.TotalDeliveries.Should().Be(1);
        stats.SuccessfulDeliveries.Should().Be(1);
        stats.SuccessRatePercentage.Should().Be(100.0);
        stats.AverageLatencyMs.Should().Be(25.0);
    }

    [Fact]
    public async Task Delivery_Single_Replay_And_Lineage_Tracking()
    {
        // Arrange
        var slug = $"rep-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Replay Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Replay App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.github.com/webhook", "Replay EP", 600, 15, new List<string> { "payment.*" }));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // 1. Publish Event to create Delivery #1
        var payload = JsonDocument.Parse("{\"paymentId\":\"pay_555\",\"amount\":120.00}").RootElement;
        var pubRes = await _client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("payment.failed", payload));
        pubRes.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var listRes = await _client.GetAsync("/api/v1/deliveries");
        var paged = await listRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        var originalDeliveryId = paged!.Items[0].Id;

        // 2. Record failed attempt on original delivery
        await _client.PostAsJsonAsync($"/api/v1/deliveries/{originalDeliveryId}/attempts", new RecordDeliveryAttemptCommand(
            504, "{}", "{\"paymentId\":\"pay_555\"}", null, null, 10000, "Gateway Timeout", DeliveryStatus.Failed));

        // 3. Trigger Single Replay
        var replayRes = await _client.PostAsJsonAsync($"/api/v1/deliveries/{originalDeliveryId}/replay", new ReplayDeliveryCommand());
        replayRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var replay = await replayRes.Content.ReadFromJsonAsync<ReplayDeliveryResponse>(JsonOptions);
        replay.Should().NotBeNull();
        replay!.OriginalDeliveryId.Should().Be(originalDeliveryId);
        replay.DeliveryId.Should().NotBe(originalDeliveryId);
        replay.Status.Should().Be(DeliveryStatus.Pending);

        // 4. Query Delivery Lineage
        var lineageRes = await _client.GetAsync($"/api/v1/deliveries/{replay.DeliveryId}/lineage");
        lineageRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var lineage = await lineageRes.Content.ReadFromJsonAsync<DeliveryLineageResponse>(JsonOptions);
        lineage.Should().NotBeNull();
        lineage!.RootDeliveryId.Should().Be(originalDeliveryId);
        lineage.LineageChain.Should().HaveCount(2);
        lineage.LineageChain[0].Id.Should().Be(originalDeliveryId);
        lineage.LineageChain[1].Id.Should().Be(replay.DeliveryId);
    }

    [Fact]
    public async Task Delivery_Bulk_Replay_Failed_Deliveries()
    {
        // Arrange
        var slug = $"blk-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Bulk Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Bulk App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.github.com/webhook", "Bulk EP", 600, 15, new List<string> { "order.*" }));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // Publish 3 events
        for (int i = 1; i <= 3; i++)
        {
            var payload = JsonDocument.Parse($"{{\"orderId\":{i}}}").RootElement;
            await _client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("order.placed", payload));
        }

        var listRes = await _client.GetAsync("/api/v1/deliveries?pageSize=10");
        var paged = await listRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        paged!.Items.Should().HaveCount(3);

        // Mark 2 failed, 1 success
        await _client.PostAsJsonAsync($"/api/v1/deliveries/{paged.Items[0].Id}/attempts", new RecordDeliveryAttemptCommand(
            500, "{}", "{}", null, null, 100, "Error", DeliveryStatus.Failed));
        await _client.PostAsJsonAsync($"/api/v1/deliveries/{paged.Items[1].Id}/attempts", new RecordDeliveryAttemptCommand(
            500, "{}", "{}", null, null, 100, "Error", DeliveryStatus.Failed));
        await _client.PostAsJsonAsync($"/api/v1/deliveries/{paged.Items[2].Id}/attempts", new RecordDeliveryAttemptCommand(
            200, "{}", "{}", null, null, 50, null, DeliveryStatus.Success));

        // Act - Bulk Replay with default filter (Failed/DeadLettered)
        var bulkRes = await _client.PostAsJsonAsync("/api/v1/deliveries/replay", new BulkReplayDeliveriesCommand());
        bulkRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var bulk = await bulkRes.Content.ReadFromJsonAsync<BulkReplayDeliveriesResponse>(JsonOptions);

        // Assert
        bulk.Should().NotBeNull();
        bulk!.ReplayedCount.Should().Be(2);
        bulk.ReplayedDeliveries.Should().HaveCount(2);
    }
}
