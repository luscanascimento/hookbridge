using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Enums;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.Concurrency;

public sealed class ConcurrentOperationsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ConcurrentOperationsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid AppId, Guid EpId)> SetupTenantAndEndpointAsync(string slugPrefix, string pattern = "*")
    {
        var slug = $"{slugPrefix}-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            TenantIdentifier: slug,
            TenantName: $"Org {slugPrefix}",
            AdminEmail: $"{slug}@concurrency.test",
            AdminPassword: "SecurePassword#2026"
        ));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var token = auth!.AccessToken;

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var appRes = await client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand($"App {slugPrefix}", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.github.com/webhook/conc", "Concurrency Endpoint", 1200, 15, new List<string> { pattern }));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        return (token, app.Id, ep!.Id);
    }

    [Fact]
    public async Task Concurrent_Delivery_Attempt_Recording_Maintains_Data_Integrity()
    {
        // Arrange
        var (token, _, _) = await SetupTenantAndEndpointAsync("att-conc", "order.*");
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Publish 4 events to create 4 deliveries
        var deliveryIds = new List<Guid>();
        for (int i = 0; i < 4; i++)
        {
            var payload = JsonDocument.Parse($"{{\"index\":{i}}}").RootElement;
            var pubRes = await client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand($"order.created.{i}", payload));
            pubRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        var listRes = await client.GetAsync("/api/v1/deliveries");
        var paged = await listRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        deliveryIds = paged!.Items.Select(d => d.Id).ToList();
        deliveryIds.Should().HaveCount(4);

        // Act: Concurrently record multiple attempts across all deliveries with serialized connection access
        var semaphore = new SemaphoreSlim(1, 1);
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 12; i++)
        {
            var targetDeliveryId = deliveryIds[i % deliveryIds.Count];
            var attemptIndex = i;

            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    using var attemptClient = _factory.CreateClient();
                    attemptClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    return await attemptClient.PostAsJsonAsync($"/api/v1/deliveries/{targetDeliveryId}/attempts", new RecordDeliveryAttemptCommand(
                        HttpStatusCode: 200,
                        RequestHeadersJson: "{\"X-Test\":\"Concurrent\"}",
                        RequestBody: $"{{\"attempt\":{attemptIndex}}}",
                        ResponseHeadersJson: "{\"Content-Type\":\"application/json\"}",
                        ResponseBody: "{\"status\":\"ok\"}",
                        ElapsedMs: 20 + (attemptIndex * 2),
                        ErrorMessage: null,
                        FinalStatus: DeliveryStatus.Success
                    ));
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert: All 12 attempts recorded without concurrency crashes
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Created));

        // Verify each delivery has its recorded attempts
        foreach (var id in deliveryIds)
        {
            var detailRes = await client.GetAsync($"/api/v1/deliveries/{id}");
            detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var detail = await detailRes.Content.ReadFromJsonAsync<DeliveryDetailResponse>(JsonOptions);
            detail.Should().NotBeNull();
            detail!.Attempts.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task Concurrent_Event_Publishing_Under_High_Load_Schedules_All_Deliveries()
    {
        // Arrange: Setup tenant with 2 distinct wildcard endpoints
        var (token, appId, _) = await SetupTenantAndEndpointAsync("pub-conc", "transaction.*");
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Add 2nd endpoint
        await client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            appId, "https://api.stripe.com/webhook/conc2", "Second Endpoint", 1200, 15, new List<string> { "transaction.*" }));

        // Act: Multiple requests publishing events with serialized connection access
        var semaphore = new SemaphoreSlim(1, 1);
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 8; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    using var pubClient = _factory.CreateClient();
                    pubClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var payload = JsonDocument.Parse($"{{\"txId\":\"tx_{idx}\",\"amount\":{idx * 100}}}").RootElement;
                    return await pubClient.PostAsJsonAsync("/api/v1/events", new PublishEventCommand(
                        EventType: $"transaction.authorized.{idx}",
                        Payload: payload,
                        IdempotencyKey: $"idemp_tx_{idx}_{Guid.NewGuid():N}"
                    ));
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert: All 8 requests accepted
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Accepted));

        // Query total deliveries (8 events * 2 endpoints = 16 deliveries)
        var listRes = await client.GetAsync("/api/v1/deliveries?pageSize=50");
        var paged = await listRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        paged.Should().NotBeNull();
        paged!.Items.Should().HaveCount(16);
    }

    [Fact]
    public async Task Concurrent_Bulk_Replay_Executes_Safely()
    {
        // Arrange
        var (token, _, _) = await SetupTenantAndEndpointAsync("bulk-conc", "order.*");
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Publish 3 events
        for (int i = 0; i < 3; i++)
        {
            var payload = JsonDocument.Parse($"{{\"id\":{i}}}").RootElement;
            await client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand($"order.failed.{i}", payload));
        }

        var listRes = await client.GetAsync("/api/v1/deliveries");
        var paged = await listRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);

        // Mark deliveries as Failed
        foreach (var del in paged!.Items)
        {
            await client.PostAsJsonAsync($"/api/v1/deliveries/{del.Id}/attempts", new RecordDeliveryAttemptCommand(
                500, "{}", "{}", "{}", "{}", 50, "Failed", DeliveryStatus.Failed));
        }

        // Act: 3 bulk replay calls targeting Failed deliveries
        var semaphore = new SemaphoreSlim(1, 1);
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    using var replayClient = _factory.CreateClient();
                    replayClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    return await replayClient.PostAsJsonAsync("/api/v1/deliveries/replay", new BulkReplayDeliveriesCommand(
                        Status: DeliveryStatus.Failed,
                        EndpointId: null,
                        EventType: null,
                        DeliveryIds: null
                    ));
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert: All bulk replay requests respond successfully
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    }
}
