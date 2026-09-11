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

public sealed class SignalRIsolationAndStressTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SignalRIsolationAndStressTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterTenantAsync(string prefix)
    {
        var slug = $"{prefix.ToLowerInvariant()}-{Guid.NewGuid():N}"[..18];
        var email = $"{slug}@example.test";
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, $"{prefix} Corp", email, "Password#2026"));

        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
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
    public async Task CrossTenant_SignalR_Isolation_Ensures_Zero_Event_Leakage()
    {
        // 1. Tenant A setup & SignalR connection
        var tokenA = await RegisterTenantAsync("iso-a");
        var hubA = BuildHubConnection(tokenA);
        var eventsReceivedByA = new List<RealtimeDeliveryEvent>();
        hubA.On<RealtimeDeliveryEvent>("ReceiveDeliveryEvent", evt =>
        {
            lock (eventsReceivedByA)
            {
                eventsReceivedByA.Add(evt);
            }
        });
        await hubA.StartAsync();

        // 2. Tenant B setup & SignalR connection
        var tokenB = await RegisterTenantAsync("iso-b");
        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var appRes = await clientB.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Tenant B App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        await clientB.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.tenantb.com/webhook", "Ep B", 600, 15, new List<string> { "*" }));

        // 3. Tenant B publishes an event
        var payload = JsonDocument.Parse("{\"tenant\":\"B\",\"data\":\"secret_b_data\"}").RootElement;
        await clientB.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("tenant.b.event", payload));

        // Allow time for async SignalR propagation
        await Task.Delay(300);

        // 4. Assert: Tenant A MUST receive ZERO events from Tenant B
        lock (eventsReceivedByA)
        {
            eventsReceivedByA.Should().BeEmpty();
        }

        await hubA.StopAsync();
    }

    [Fact]
    public async Task Multiple_Connections_For_Same_Tenant_All_Receive_Broadcast()
    {
        // 1. Tenant setup
        var token = await RegisterTenantAsync("multi-conn");
        using var apiClient = _factory.CreateClient();
        apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var appRes = await apiClient.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Multi App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        await apiClient.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.multiconn.com/webhook", "Ep", 600, 15, new List<string> { "*" }));

        // 2. Create 3 parallel SignalR client connections for this same tenant
        var hub1 = BuildHubConnection(token);
        var hub2 = BuildHubConnection(token);
        var hub3 = BuildHubConnection(token);

        var count1 = 0;
        var count2 = 0;
        var count3 = 0;

        hub1.On<RealtimeDeliveryEvent>("ReceiveDeliveryEvent", _ => Interlocked.Increment(ref count1));
        hub2.On<RealtimeDeliveryEvent>("ReceiveDeliveryEvent", _ => Interlocked.Increment(ref count2));
        hub3.On<RealtimeDeliveryEvent>("ReceiveDeliveryEvent", _ => Interlocked.Increment(ref count3));

        await Task.WhenAll(hub1.StartAsync(), hub2.StartAsync(), hub3.StartAsync());

        // 3. Publish event
        var payload = JsonDocument.Parse("{\"test\":\"broadcast\"}").RootElement;
        await apiClient.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("test.broadcast", payload));

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && (count1 == 0 || count2 == 0 || count3 == 0))
        {
            await Task.Delay(50);
        }

        // 4. Assert: All 3 connections received the event
        count1.Should().BeGreaterThanOrEqualTo(1);
        count2.Should().BeGreaterThanOrEqualTo(1);
        count3.Should().BeGreaterThanOrEqualTo(1);

        await Task.WhenAll(hub1.StopAsync(), hub2.StopAsync(), hub3.StopAsync());
    }

    [Fact]
    public async Task Rapid_Connect_Disconnect_Cycles_Do_Not_Throw_Or_Leak()
    {
        var token = await RegisterTenantAsync("rapid-conn");

        for (int i = 0; i < 5; i++)
        {
            var hub = BuildHubConnection(token);
            await hub.StartAsync();
            hub.State.Should().Be(HubConnectionState.Connected);
            await hub.StopAsync();
            hub.State.Should().Be(HubConnectionState.Disconnected);
            await hub.DisposeAsync();
        }
    }
}
