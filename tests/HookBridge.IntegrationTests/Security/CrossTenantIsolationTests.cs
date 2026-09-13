using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Persistence;
using HookBridge.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.IntegrationTests.Security;

public class CrossTenantIsolationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CrossTenantIsolationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AuthenticatedUser_CannotSpoofTenantIdViaHeader()
    {
        // Arrange 1: Register Tenant A
        var slugA = $"spoof-a-{Guid.NewGuid():N}"[..16];
        var regResponseA = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slugA, "Tenant A", $"admin@{slugA}.test", "Password#2026"));
        var authA = await regResponseA.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        // Arrange 2: Register Tenant B
        var slugB = $"spoof-b-{Guid.NewGuid():N}"[..16];
        var regResponseB = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slugB, "Tenant B", $"admin@{slugB}.test", "Password#2026"));
        var authB = await regResponseB.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        // Act: User from Tenant B sends request with Tenant A's ID in X-Tenant-ID header
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authB!.AccessToken);
        request.Headers.Add("X-Tenant-ID", authA!.User.TenantId.ToString());

        var response = await _client.SendAsync(request);

        // Assert: System must respect JWT claims, rejecting the forged header override
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);
        profile.Should().NotBeNull();
        profile!.TenantId.Should().Be(authB.User.TenantId);
        profile.TenantIdentifier.Should().Be(slugB);
        profile.TenantId.Should().NotBe(authA.User.TenantId);
    }

    [Fact]
    public async Task EFCore_GlobalQueryFilter_PreventsCrossTenantDataRead()
    {
        // Arrange: Direct DB setup with two tenants
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HookBridgeDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        var now = DateTimeOffset.UtcNow;

        var tenantA = Tenant.Create("ef-tenant-a", "EF Tenant A", now).Value;
        var tenantB = Tenant.Create("ef-tenant-b", "EF Tenant B", now).Value;

        var userA = User.Create(tenantA.Id, "usera@ef.test", "hash", UserRole.Developer, now).Value;
        var userB = User.Create(tenantB.Id, "userb@ef.test", "hash", UserRole.Developer, now).Value;

        db.Tenants.AddRange(tenantA, tenantB);
        db.Users.AddRange(userA, userB);
        await db.SaveChangesAsync();

        // Act: Scope to Tenant A
        tenantContext.SetTenant(tenantA.Id, tenantA.Identifier);

        // Query users through EF Core with global query filter active
        var visibleUsersForTenantA = await db.Users.ToListAsync();

        // Assert: Tenant A can only see user A, never user B
        visibleUsersForTenantA.Should().ContainSingle(u => u.Email == "usera@ef.test");
        visibleUsersForTenantA.Should().NotContain(u => u.Email == "userb@ef.test");
    }

    private async Task<(string Token, Guid TenantId, string Slug)> RegisterTenantAsync(string prefix)
    {
        var slug = $"{prefix.ToLowerInvariant()}-{Guid.NewGuid():N}"[..16];
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, $"Tenant {prefix}", $"{slug}@isolation.test", "Password#2026"));

        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return (auth!.AccessToken, auth.User.TenantId, slug);
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task CrossTenant_Application_Access_And_Mutation_IsStrictlyIsolated_Against_IDOR()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterTenantAsync("app-iso-a");
        var (tokenB, _, _) = await RegisterTenantAsync("app-iso-b");

        using var clientA = CreateAuthenticatedClient(tokenA);
        using var clientB = CreateAuthenticatedClient(tokenB);

        var appRes = await clientA.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Tenant A Core App", "Confidential"));
        appRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var appA = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        // Act & Assert 1: Tenant B tries to GET Tenant A's Application -> 404
        var getRes = await clientB.GetAsync($"/api/v1/apps/{appA!.Id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Act & Assert 2: Tenant B tries to PUT (modify) Tenant A's Application -> 404
        var putRes = await clientB.PutAsJsonAsync($"/api/v1/apps/{appA.Id}", new UpdateApplicationCommand("Compromised Name", "Tampered"));
        putRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Act & Assert 3: Tenant B tries to DELETE Tenant A's Application -> 404
        var delRes = await clientB.DeleteAsync($"/api/v1/apps/{appA.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Verify: Tenant A's application remains completely intact
        var verifyRes = await clientA.GetAsync($"/api/v1/apps/{appA.Id}");
        verifyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var currentApp = await verifyRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);
        currentApp!.Name.Should().Be("Tenant A Core App");
    }

    [Fact]
    public async Task CrossTenant_Endpoint_Access_Mutation_And_SecretRotation_AreStrictlyIsolated_Against_IDOR()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterTenantAsync("ep-iso-a");
        var (tokenB, _, _) = await RegisterTenantAsync("ep-iso-b");

        using var clientA = CreateAuthenticatedClient(tokenA);
        using var clientB = CreateAuthenticatedClient(tokenB);

        var appRes = await clientA.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Invoicing App", "App for invoices"));
        var appA = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await clientA.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: appA!.Id,
            TargetUrl: "https://api.github.com/webhook/tenant-a",
            Description: "Tenant A Webhook",
            RateLimitPerMinute: 300,
            TimeoutSeconds: 12,
            SubscribedEvents: new List<string> { "invoice.*" }));
        epRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var epA = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // Act & Assert 1: Tenant B tries to GET Tenant A's Endpoint -> 404
        var getRes = await clientB.GetAsync($"/api/v1/endpoints/{epA!.Id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Act & Assert 2: Tenant B tries to PUT (modify) Tenant A's Endpoint -> 404
        var putRes = await clientB.PutAsJsonAsync($"/api/v1/endpoints/{epA.Id}", new UpdateEndpointCommand("https://attacker.com/sink", "Hacked"));
        putRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Act & Assert 3: Tenant B tries to list or rotate Tenant A's Webhook Secrets -> 404
        var listSecretsRes = await clientB.GetAsync($"/api/v1/endpoints/{epA.Id}/secrets");
        listSecretsRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var rotateRes = await clientB.PostAsync($"/api/v1/endpoints/{epA.Id}/secrets/rotate", null);
        rotateRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Act & Assert 4: Tenant B tries to DELETE Tenant A's Endpoint -> 404
        var delRes = await clientB.DeleteAsync($"/api/v1/endpoints/{epA.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Verify: Tenant A's endpoint remains untouched
        var verifyRes = await clientA.GetAsync($"/api/v1/endpoints/{epA.Id}");
        verifyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var currentEp = await verifyRes.Content.ReadFromJsonAsync<EndpointResponse>(JsonOptions);
        currentEp!.TargetUrl.Should().Be("https://api.github.com/webhook/tenant-a");
        currentEp.ActiveSecretVersion.Should().Be(1);
    }

    [Fact]
    public async Task CrossTenant_ApiKey_Revocation_IsStrictlyIsolated_Against_IDOR()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterTenantAsync("key-iso-a");
        var (tokenB, _, _) = await RegisterTenantAsync("key-iso-b");

        using var clientA = CreateAuthenticatedClient(tokenA);
        using var clientB = CreateAuthenticatedClient(tokenB);

        var keyRes = await clientA.PostAsJsonAsync("/api/v1/api-keys", new CreateApiKeyCommand(
            Name: "Tenant A Production Key",
            Scopes: ApiKeyScope.EventsIngest | ApiKeyScope.DeliveriesRead));
        keyRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var keyA = await keyRes.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions);

        // Act: Tenant B attempts to revoke Tenant A's API Key -> 404
        var revokeRes = await clientB.DeleteAsync($"/api/v1/api-keys/{keyA!.Id}");
        revokeRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Verify: Tenant A checks their keys; Key A is still active and not revoked
        var listRes = await clientA.GetAsync("/api/v1/api-keys");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var keys = await listRes.Content.ReadFromJsonAsync<IReadOnlyList<ApiKeyResponse>>(JsonOptions);
        var matchingKey = keys!.FirstOrDefault(k => k.Id == keyA.Id);
        matchingKey.Should().NotBeNull();
        matchingKey!.IsActive.Should().BeTrue();
        matchingKey.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task CrossTenant_Delivery_Inspection_And_AttemptRecording_AreStrictlyIsolated()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterTenantAsync("del-iso-a");
        var (tokenB, _, _) = await RegisterTenantAsync("del-iso-b");

        using var clientA = CreateAuthenticatedClient(tokenA);
        using var clientB = CreateAuthenticatedClient(tokenB);

        var appRes = await clientA.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Order App", "Orders"));
        var appA = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await clientA.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: appA!.Id,
            TargetUrl: "https://api.github.com/webhook/orders-iso",
            Description: "Order Endpoint",
            RateLimitPerMinute: 600,
            TimeoutSeconds: 10,
            SubscribedEvents: new List<string> { "order.*" }));
        epRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = JsonDocument.Parse("{\"orderId\":\"ord_iso_999\",\"total\":150.00}").RootElement;
        var pubRes = await clientA.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("order.placed", payload));
        pubRes.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var deliveriesRes = await clientA.GetAsync("/api/v1/deliveries");
        var deliveries = await deliveriesRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        deliveries!.Items.Should().NotBeEmpty();
        var deliveryA = deliveries.Items[0];

        // Act & Assert 1: Tenant B tries to inspect Tenant A's Delivery -> 404
        var getDelRes = await clientB.GetAsync($"/api/v1/deliveries/{deliveryA.Id}");
        getDelRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Act & Assert 2: Tenant B tries to record an attempt on Tenant A's Delivery -> 404
        var attemptRes = await clientB.PostAsJsonAsync($"/api/v1/deliveries/{deliveryA.Id}/attempts", new RecordDeliveryAttemptCommand(
            HttpStatusCode: 200,
            RequestHeadersJson: "{}",
            RequestBody: "{}",
            ResponseHeadersJson: "{}",
            ResponseBody: "{}",
            ElapsedMs: 15,
            ErrorMessage: null,
            FinalStatus: DeliveryStatus.Success));
        attemptRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrossTenant_DeliveryReplay_And_Lineage_AreStrictlyBlocked()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterTenantAsync("rep-iso-a");
        var (tokenB, _, _) = await RegisterTenantAsync("rep-iso-b");

        using var clientA = CreateAuthenticatedClient(tokenA);
        using var clientB = CreateAuthenticatedClient(tokenB);

        var appResA = await clientA.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Payment App A", "Tenant A Payments"));
        var appA = await appResA.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epResA = await clientA.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: appA!.Id,
            TargetUrl: "https://api.github.com/webhook/pay-a",
            Description: "Payment A Endpoint",
            RateLimitPerMinute: 600,
            TimeoutSeconds: 10,
            SubscribedEvents: new List<string> { "payment.*" }));
        var epA = await epResA.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // Tenant B creates an endpoint as well
        var appResB = await clientB.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Payment App B", "Tenant B Payments"));
        var appB = await appResB.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epResB = await clientB.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: appB!.Id,
            TargetUrl: "https://api.github.com/webhook/pay-b",
            Description: "Payment B Endpoint",
            RateLimitPerMinute: 600,
            TimeoutSeconds: 10,
            SubscribedEvents: new List<string> { "payment.*" }));
        var epB = await epResB.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        var payload = JsonDocument.Parse("{\"paymentId\":\"pay_999\"}").RootElement;
        await clientA.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("payment.completed", payload));

        var listResA = await clientA.GetAsync("/api/v1/deliveries");
        var deliveriesA = await listResA.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        var deliveryA = deliveriesA!.Items[0];

        // Act & Assert 1: Tenant B tries to replay Tenant A's Delivery -> 404
        var replayResB = await clientB.PostAsJsonAsync($"/api/v1/deliveries/{deliveryA.Id}/replay", new ReplayDeliveryCommand());
        replayResB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Act & Assert 2: Tenant B tries to query Tenant A's Delivery lineage -> 404
        var lineageResB = await clientB.GetAsync($"/api/v1/deliveries/{deliveryA.Id}/lineage");
        lineageResB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Act & Assert 3: Tenant A attempts to replay Delivery A, but redirecting to Tenant B's Endpoint B -> 404
        var redirectReplayRes = await clientA.PostAsJsonAsync($"/api/v1/deliveries/{deliveryA.Id}/replay", new ReplayDeliveryCommand(
            OverrideEndpointId: epB!.Id));
        redirectReplayRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrossTenant_BulkReplay_SilentlyFilters_ForeignDeliveries()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterTenantAsync("bulk-iso-a");
        var (tokenB, _, _) = await RegisterTenantAsync("bulk-iso-b");

        using var clientA = CreateAuthenticatedClient(tokenA);
        using var clientB = CreateAuthenticatedClient(tokenB);

        var appResA = await clientA.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Store App A", "Store A"));
        var appA = await appResA.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        await clientA.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: appA!.Id,
            TargetUrl: "https://api.github.com/webhook/store-a",
            Description: "Store A Endpoint",
            RateLimitPerMinute: 600,
            TimeoutSeconds: 10,
            SubscribedEvents: new List<string> { "store.*" }));

        var payload = JsonDocument.Parse("{\"storeId\":\"store_101\"}").RootElement;
        await clientA.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("store.inventory.low", payload));

        var listResA = await clientA.GetAsync("/api/v1/deliveries");
        var deliveriesA = await listResA.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        var deliveryA = deliveriesA!.Items[0];

        // Act: Tenant B executes bulk replay specifying Tenant A's DeliveryId
        var bulkRes = await clientB.PostAsJsonAsync("/api/v1/deliveries/replay", new BulkReplayDeliveriesCommand(
            DeliveryIds: new List<Guid> { deliveryA.Id }));

        // Assert: Bulk replay succeeds but replayed count is 0 because deliveryA is outside Tenant B's boundary
        bulkRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var bulkResponse = await bulkRes.Content.ReadFromJsonAsync<BulkReplayDeliveriesResponse>(JsonOptions);
        bulkResponse!.ReplayedCount.Should().Be(0);

        // Verify: Tenant A's delivery lineage has count 1 (no replays were performed)
        var lineageResA = await clientA.GetAsync($"/api/v1/deliveries/{deliveryA.Id}/lineage");
        lineageResA.StatusCode.Should().Be(HttpStatusCode.OK);
        var lineageA = await lineageResA.Content.ReadFromJsonAsync<DeliveryLineageResponse>(JsonOptions);
        lineageA!.LineageChain.Should().HaveCount(1);
    }

    [Fact]
    public async Task CrossTenant_EndpointHealth_And_Traces_AreStrictlyIsolated()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterTenantAsync("health-iso-a");
        var (tokenB, _, _) = await RegisterTenantAsync("health-iso-b");

        using var clientA = CreateAuthenticatedClient(tokenA);
        using var clientB = CreateAuthenticatedClient(tokenB);

        var appResA = await clientA.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Telemetry App", "App"));
        var appA = await appResA.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epResA = await clientA.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: appA!.Id,
            TargetUrl: "https://api.github.com/webhook/telemetry-a",
            Description: "Telemetry Endpoint",
            RateLimitPerMinute: 600,
            TimeoutSeconds: 10,
            SubscribedEvents: new List<string> { "telemetry.*" }));
        var epA = await epResA.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        var uniqueCorrelationId = $"corr-iso-unique-{Guid.NewGuid():N}";
        var payload = JsonDocument.Parse("{\"metric\":\"cpu_load\"}").RootElement;
        await clientA.PostAsJsonAsync("/api/v1/events", new PublishEventCommand(
            EventType: "telemetry.recorded",
            Payload: payload,
            CorrelationId: uniqueCorrelationId));

        var listResA = await clientA.GetAsync("/api/v1/deliveries");
        var deliveriesA = await listResA.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        var deliveryA = deliveriesA!.Items[0];

        // Act & Assert 1: Tenant B tries to query Tenant A's Endpoint Health -> 404
        var healthResB = await clientB.GetAsync($"/api/v1/endpoints/{epA!.Id}/health");
        healthResB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Act & Assert 2: Tenant B tries to query Tenant A's Trace by DeliveryId -> 404
        var traceDetailResB = await clientB.GetAsync($"/api/v1/traces/{deliveryA.Id}");
        traceDetailResB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Act & Assert 3: Tenant B searches traces by Tenant A's unique CorrelationId -> 0 items
        var traceSearchResB = await clientB.GetAsync($"/api/v1/traces?query={uniqueCorrelationId}");
        traceSearchResB.StatusCode.Should().Be(HttpStatusCode.OK);
        var traceSearchB = await traceSearchResB.Content.ReadFromJsonAsync<PagedList<TraceSummaryResponse>>(JsonOptions);
        traceSearchB!.Items.Should().BeEmpty();
    }
}
