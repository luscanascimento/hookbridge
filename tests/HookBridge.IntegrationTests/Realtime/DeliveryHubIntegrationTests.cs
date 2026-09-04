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
using Microsoft.AspNetCore.SignalR.Client;

namespace HookBridge.IntegrationTests.Realtime;

public class DeliveryHubIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DeliveryHubIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid TenantId, Guid UserId)> RegisterTenantAsync(string prefix)
    {
        var slug = $"{prefix.ToLowerInvariant()}-{Guid.NewGuid():N}"[..18];
        var email = $"{slug}@example.com";
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, $"{prefix} Corp", email, "Password#2026"));

        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        return (auth!.AccessToken, auth.User.TenantId, auth.User.UserId);
    }

    private HubConnection BuildHubConnection(string token)
    {
        return new HubConnectionBuilder()
            .WithUrl(
                new Uri(_factory.Server.BaseAddress, "hubs/deliveries"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                })
            .Build();
    }

    [Fact]
    public async Task SignalR_ConnectWithoutToken_ShouldFailOrBeRejected()
    {
        // Arrange
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(_factory.Server.BaseAddress, "hubs/deliveries"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
            .Build();

        // Act & Assert
        var act = () => hubConnection.StartAsync();
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SignalR_ConnectWithValidToken_ShouldSucceedAndReceiveEvents()
    {
        // Arrange
        var (token, _, _) = await RegisterTenantAsync("sigconn");
        var hubConnection = BuildHubConnection(token);

        var receivedEvents = new List<RealtimeDeliveryEvent>();
        hubConnection.On<RealtimeDeliveryEvent>("ReceiveDeliveryEvent", evt =>
        {
            lock (receivedEvents)
            {
                receivedEvents.Add(evt);
            }
        });

        // Act
        await hubConnection.StartAsync();
        hubConnection.State.Should().Be(HubConnectionState.Connected);

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task SignalR_PublishAndAttemptAndReplay_ShouldBroadcastRealtimeEvents()
    {
        // Arrange
        var (token, tenantId, _) = await RegisterTenantAsync("sigstream");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Create Application & Endpoint with subscription
        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Order App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id,
            "https://api.github.com/webhooks",
            "Order Webhooks",
            600,
            15,
            new List<string> { "order.*" }));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // 2. Connect SignalR client
        var hubConnection = BuildHubConnection(token);
        var receivedEvents = new List<RealtimeDeliveryEvent>();
        var dispatchedEvents = new List<RealtimeDeliveryEvent>();
        var attemptEvents = new List<RealtimeDeliveryEvent>();
        var replayedEvents = new List<RealtimeDeliveryEvent>();

        hubConnection.On<RealtimeDeliveryEvent>("ReceiveDeliveryEvent", evt =>
        {
            lock (receivedEvents) { receivedEvents.Add(evt); }
        });
        hubConnection.On<RealtimeDeliveryEvent>("DeliveryDispatched", evt =>
        {
            lock (dispatchedEvents) { dispatchedEvents.Add(evt); }
        });
        hubConnection.On<RealtimeDeliveryEvent>("DeliveryAttemptRecorded", evt =>
        {
            lock (attemptEvents) { attemptEvents.Add(evt); }
        });
        hubConnection.On<RealtimeDeliveryEvent>("DeliveryReplayed", evt =>
        {
            lock (replayedEvents) { replayedEvents.Add(evt); }
        });

        await hubConnection.StartAsync();

        // 3. Publish Event
        var payload = JsonDocument.Parse("{\"orderId\":\"ord_live_1\"}").RootElement;
        var pubRes = await _client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand(
            EventType: "order.created",
            Payload: payload,
            IdempotencyKey: "idemp_signalr_1",
            Version: 1,
            CorrelationId: "corr_signalr_1"));
        pubRes.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Allow SignalR message loop
        await Task.Delay(250);

        Guid deliveryId;
        lock (dispatchedEvents)
        {
            dispatchedEvents.Should().ContainSingle();
            dispatchedEvents[0].EventName.Should().Be("order.created");
            dispatchedEvents[0].EndpointId.Should().Be(ep!.Id);
            dispatchedEvents[0].TenantId.Should().Be(tenantId);
            deliveryId = dispatchedEvents[0].DeliveryId;
        }

        // 4. Record Attempt
        var attemptRes = await _client.PostAsJsonAsync($"/api/v1/deliveries/{deliveryId}/attempts", new RecordDeliveryAttemptCommand(
            200, "{\"Content-Type\":\"application/json\"}", "{\"orderId\":\"ord_live_1\"}",
            "{\"status\":\"ok\"}", "{\"success\":true}", 42, null, DeliveryStatus.Success));
        attemptRes.StatusCode.Should().Be(HttpStatusCode.Created);

        await Task.Delay(250);

        lock (attemptEvents)
        {
            attemptEvents.Should().ContainSingle();
            attemptEvents[0].DeliveryId.Should().Be(deliveryId);
            attemptEvents[0].Attempt.Should().NotBeNull();
            attemptEvents[0].Attempt!.HttpStatusCode.Should().Be(200);
            attemptEvents[0].Attempt!.ElapsedMs.Should().Be(42);
        }

        // 5. Replay Delivery
        var replayRes = await _client.PostAsJsonAsync($"/api/v1/deliveries/{deliveryId}/replay", (ReplayDeliveryCommand?)null);
        replayRes.StatusCode.Should().Be(HttpStatusCode.OK);

        await Task.Delay(250);

        lock (replayedEvents)
        {
            replayedEvents.Should().ContainSingle();
            replayedEvents[0].OriginalDeliveryId.Should().Be(deliveryId);
            replayedEvents[0].EventName.Should().Be("order.created");
        }

        await hubConnection.StopAsync();
    }

    [Fact]
    public async Task SignalR_TenantIsolation_ShouldNotLeakEventsAcrossTenants()
    {
        // Arrange
        var (tokenTenantA, tenantAId, _) = await RegisterTenantAsync("tenanta");
        var (tokenTenantB, _, _) = await RegisterTenantAsync("tenantb");

        // Set up Endpoint for Tenant A
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenTenantA);
        var appA = (await (await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Tenant A App", null)))
            .Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions))!;
        await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            appA.Id, "https://api.github.com/webhook-a", "Webhook A", 600, 15, new List<string> { "tenant.event" }));

        // Connect Hub Clients
        var hubA = BuildHubConnection(tokenTenantA);
        var hubB = BuildHubConnection(tokenTenantB);

        var eventsReceivedByA = new List<RealtimeDeliveryEvent>();
        var eventsReceivedByB = new List<RealtimeDeliveryEvent>();

        hubA.On<RealtimeDeliveryEvent>("ReceiveDeliveryEvent", evt =>
        {
            lock (eventsReceivedByA) { eventsReceivedByA.Add(evt); }
        });
        hubB.On<RealtimeDeliveryEvent>("ReceiveDeliveryEvent", evt =>
        {
            lock (eventsReceivedByB) { eventsReceivedByB.Add(evt); }
        });

        await hubA.StartAsync();
        await hubB.StartAsync();

        // Act - Publish event on Tenant A
        var payload = JsonDocument.Parse("{\"secret\":\"data_for_tenant_a\"}").RootElement;
        await _client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand(
            EventType: "tenant.event",
            Payload: payload,
            IdempotencyKey: "idemp_a_1"));

        await Task.Delay(250);

        // Assert
        lock (eventsReceivedByA)
        {
            eventsReceivedByA.Should().ContainSingle();
            eventsReceivedByA[0].TenantId.Should().Be(tenantAId);
        }

        lock (eventsReceivedByB)
        {
            eventsReceivedByB.Should().BeEmpty("Tenant B must NEVER receive Tenant A's real-time webhook events!");
        }

        await hubA.StopAsync();
        await hubB.StopAsync();
    }

    [Fact]
    public async Task SignalR_SubscribeToEndpoint_AuthorizationCheck()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterTenantAsync("suba");
        var (tokenB, _, _) = await RegisterTenantAsync("subb");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var appA = (await (await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("App A", null)))
            .Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions))!;
        var epA = (await (await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            appA.Id, "https://api.github.com/hook-sub", "EP A", 600, 15, new List<string> { "*" })))
            .Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions))!;

        var hubConnectionA = BuildHubConnection(tokenA);
        var hubConnectionB = BuildHubConnection(tokenB);

        await hubConnectionA.StartAsync();
        await hubConnectionB.StartAsync();

        // Act & Assert
        // Tenant A subscribing to Tenant A's endpoint -> Success
        var subResultA = await hubConnectionA.InvokeAsync<bool>("SubscribeToEndpoint", epA!.Id);
        subResultA.Should().BeTrue();

        // Tenant B subscribing to Tenant A's endpoint -> Rejected
        var subResultB = await hubConnectionB.InvokeAsync<bool>("SubscribeToEndpoint", epA.Id);
        subResultB.Should().BeFalse();

        // Unsubscribe
        var unsubResultA = await hubConnectionA.InvokeAsync<bool>("UnsubscribeFromEndpoint", epA.Id);
        unsubResultA.Should().BeTrue();

        await hubConnectionA.StopAsync();
        await hubConnectionB.StopAsync();
    }
}
