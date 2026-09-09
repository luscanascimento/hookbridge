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

namespace HookBridge.IntegrationTests.E2E;

public sealed class EndToEndWebhookLifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EndToEndWebhookLifecycleTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Complete_EndToEnd_Webhook_Pipeline_Flow()
    {
        // =========================================================================
        // Step 1: Register Tenant & Authenticate
        // =========================================================================
        var slug = $"e2e-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            TenantIdentifier: slug,
            TenantName: "E2E Enterprise Corp",
            AdminEmail: $"{slug}@enterprise.test",
            AdminPassword: "SecurePassword#2026"
        ));
        regRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // =========================================================================
        // Step 2: Create Application
        // =========================================================================
        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand(
            Name: "E2E Commerce Platform",
            Description: "Production e-commerce transaction gateway"
        ));
        appRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);
        app.Should().NotBeNull();

        // =========================================================================
        // Step 3: Create Endpoints with Wildcard Subscriptions
        // =========================================================================
        // Endpoint 1: Specific topic pattern "order.*"
        var ep1Res = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: app!.Id,
            TargetUrl: "https://api.github.com/webhook/orders",
            Description: "Merchant Order Webhook Receiver",
            RateLimitPerMinute: 1200,
            TimeoutSeconds: 15,
            SubscribedEvents: new List<string> { "order.*" }
        ));
        ep1Res.StatusCode.Should().Be(HttpStatusCode.Created);
        var ep1 = await ep1Res.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);
        ep1.Should().NotBeNull();
        ep1!.InitialSecret.Should().StartWith("whsec_");

        // Endpoint 2: Catch-all wildcard "*"
        var ep2Res = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: app.Id,
            TargetUrl: "https://api.stripe.com/webhook/global-events",
            Description: "Global Analytics Collector",
            RateLimitPerMinute: 3000,
            TimeoutSeconds: 10,
            SubscribedEvents: new List<string> { "*" }
        ));
        ep2Res.StatusCode.Should().Be(HttpStatusCode.Created);
        var ep2 = await ep2Res.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);
        ep2.Should().NotBeNull();

        // =========================================================================
        // Step 4: Issue Ingestion API Key
        // =========================================================================
        var keyRes = await _client.PostAsJsonAsync("/api/v1/api-keys", new CreateApiKeyCommand(
            Name: "E2E Ingestion Key",
            Scopes: ApiKeyScope.EventsIngest | ApiKeyScope.DeliveriesRead | ApiKeyScope.DeliveriesReplay,
            Environment: "live"
        ));
        keyRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var apiKey = await keyRes.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions);
        apiKey.Should().NotBeNull();
        apiKey!.Key.Should().StartWith("hb_live_");

        // =========================================================================
        // Step 5: Register Schema Definition
        // =========================================================================
        var schemaJson = """
        {
            "type": "object",
            "properties": {
                "orderId": { "type": "string" },
                "amount": { "type": "number" },
                "currency": { "type": "string" }
            },
            "required": ["orderId", "amount", "currency"]
        }
        """;
        var schemaRes = await _client.PostAsJsonAsync("/api/v1/schemas", new CreateEventSchemaRequest(
            EventType: "order.placed",
            Name: "Order Placed Schema",
            Description: "Dispatched when customer completes checkout",
            CompatibilityMode: SchemaCompatibilityMode.Backward,
            SchemaJson: schemaJson,
            Version: "1.0.0",
            VersionDescription: "Initial version",
            SamplePayloadJson: "{\"orderId\":\"123\",\"amount\":10,\"currency\":\"USD\"}"
        ));
        schemaRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // =========================================================================
        // Step 6: Publish Event (Matches both Endpoint 1 and Endpoint 2)
        // =========================================================================
        var payload = JsonDocument.Parse("""
        {
            "orderId": "ord_884920",
            "amount": 349.99,
            "currency": "USD"
        }
        """).RootElement;

        var pubRes = await _client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand(
            EventType: "order.placed",
            Payload: payload,
            IdempotencyKey: $"e2e_idemp_{Guid.NewGuid():N}"
        ));
        pubRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var pub = await pubRes.Content.ReadFromJsonAsync<PublishEventResponse>(JsonOptions);
        pub.Should().NotBeNull();
        pub!.DeliveriesScheduled.Should().Be(2); // Both ep1 ("order.*") and ep2 ("*") matched!

        // =========================================================================
        // Step 7: Inspect Deliveries List
        // =========================================================================
        var delListRes = await _client.GetAsync("/api/v1/deliveries");
        delListRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedDeliveries = await delListRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        pagedDeliveries.Should().NotBeNull();
        pagedDeliveries!.Items.Should().HaveCount(2);

        var delivery1 = pagedDeliveries.Items.First(d => d.EndpointUrl != null && d.EndpointUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase));
        var delivery2 = pagedDeliveries.Items.First(d => d.EndpointUrl != null && d.EndpointUrl.Contains("stripe.com", StringComparison.OrdinalIgnoreCase));

        // =========================================================================
        // Step 8: Record Execution Attempts (Failure then Success for Ep 1; Instant Success for Ep 2)
        // =========================================================================
        // Delivery 1 - Attempt 1: 503 Service Unavailable (Failed)
        var att1Res = await _client.PostAsJsonAsync($"/api/v1/deliveries/{delivery1.Id}/attempts", new RecordDeliveryAttemptCommand(
            HttpStatusCode: 503,
            RequestHeadersJson: "{\"X-HookBridge-Signature\":\"t=123,v1=abc\"}",
            RequestBody: "{\"orderId\":\"ord_884920\"}",
            ResponseHeadersJson: "{\"Content-Type\":\"application/json\"}",
            ResponseBody: "{\"error\":\"Service Unavailable\"}",
            ElapsedMs: 120,
            ErrorMessage: "Gateway 503 temporary error",
            FinalStatus: DeliveryStatus.Failed
        ));
        att1Res.StatusCode.Should().Be(HttpStatusCode.Created);

        // Delivery 1 - Attempt 2: 200 OK (Success)
        var att2Res = await _client.PostAsJsonAsync($"/api/v1/deliveries/{delivery1.Id}/attempts", new RecordDeliveryAttemptCommand(
            HttpStatusCode: 200,
            RequestHeadersJson: "{\"X-HookBridge-Signature\":\"t=125,v1=abc\"}",
            RequestBody: "{\"orderId\":\"ord_884920\"}",
            ResponseHeadersJson: "{\"Content-Type\":\"application/json\"}",
            ResponseBody: "{\"status\":\"ok\"}",
            ElapsedMs: 45,
            ErrorMessage: null,
            FinalStatus: DeliveryStatus.Success
        ));
        att2Res.StatusCode.Should().Be(HttpStatusCode.Created);

        // Delivery 2 - Attempt 1: 200 OK (Success)
        var att3Res = await _client.PostAsJsonAsync($"/api/v1/deliveries/{delivery2.Id}/attempts", new RecordDeliveryAttemptCommand(
            HttpStatusCode: 200,
            RequestHeadersJson: "{\"X-HookBridge-Signature\":\"t=123,v1=def\"}",
            RequestBody: "{\"orderId\":\"ord_884920\"}",
            ResponseHeadersJson: "{\"Content-Type\":\"application/json\"}",
            ResponseBody: "{\"status\":\"ok\"}",
            ElapsedMs: 30,
            ErrorMessage: null,
            FinalStatus: DeliveryStatus.Success
        ));
        att3Res.StatusCode.Should().Be(HttpStatusCode.Created);

        // =========================================================================
        // Step 9: Verify Delivery Details & Historical Attempts Timeline
        // =========================================================================
        var detailRes = await _client.GetAsync($"/api/v1/deliveries/{delivery1.Id}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailRes.Content.ReadFromJsonAsync<DeliveryDetailResponse>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(DeliveryStatus.Success);
        detail.Attempts.Should().HaveCount(2);
        detail.Attempts[0].HttpStatusCode.Should().Be(503);
        detail.Attempts[1].HttpStatusCode.Should().Be(200);

        // =========================================================================
        // Step 10: Replay Delivery & Verify Lineage Chain
        // =========================================================================
        var replayRes = await _client.PostAsync($"/api/v1/deliveries/{delivery1.Id}/replay", null);
        replayRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var replayed = await replayRes.Content.ReadFromJsonAsync<ReplayDeliveryResponse>(JsonOptions);
        replayed.Should().NotBeNull();
        replayed!.OriginalDeliveryId.Should().Be(delivery1.Id);

        var lineageRes = await _client.GetAsync($"/api/v1/deliveries/{replayed.DeliveryId}/lineage");
        lineageRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var lineage = await lineageRes.Content.ReadFromJsonAsync<DeliveryLineageResponse>(JsonOptions);
        lineage.Should().NotBeNull();
        lineage!.LineageChain.Should().Contain(a => a.Id == delivery1.Id);

        // =========================================================================
        // Step 11: Verify Aggregated Stats & Health Metrics
        // =========================================================================
        var statsRes = await _client.GetAsync("/api/v1/deliveries/stats");
        statsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await statsRes.Content.ReadFromJsonAsync<DeliveryStatsResponse>(JsonOptions);
        stats.Should().NotBeNull();
        stats!.TotalDeliveries.Should().BeGreaterThanOrEqualTo(2);
        stats.SuccessfulDeliveries.Should().BeGreaterThanOrEqualTo(2);

        var healthRes = await _client.GetAsync($"/api/v1/endpoints/{ep1.Id}/health");
        healthRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var health = await healthRes.Content.ReadFromJsonAsync<EndpointHealthResponse>(JsonOptions);
        health.Should().NotBeNull();
        health!.HealthScorePercent.Should().BeGreaterThan(0);

        // =========================================================================
        // Step 12: Verify Comprehensive Audit Trail
        // =========================================================================
        var auditRes = await _client.GetAsync("/api/v1/audit-logs");
        auditRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var auditLogs = await auditRes.Content.ReadFromJsonAsync<PagedList<AuditEntryResponse>>(JsonOptions);
        auditLogs.Should().NotBeNull();
        auditLogs!.Items.Should().Contain(a => a.Action.Contains("Application.Created", StringComparison.OrdinalIgnoreCase));
        auditLogs.Items.Should().Contain(a => a.Action.Contains("Endpoint.Created", StringComparison.OrdinalIgnoreCase));
        auditLogs.Items.Should().Contain(a => a.Action.Contains("ApiKey.Created", StringComparison.OrdinalIgnoreCase));
        auditLogs.Items.Should().Contain(a => a.Action.Contains("Delivery.Replayed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ZeroDowntime_SecretRotation_And_DualKey_Verification_E2E()
    {
        // 1. Register & setup
        var slug = $"rot-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            TenantIdentifier: slug,
            TenantName: "Rotation Corp",
            AdminEmail: $"{slug}@rotation.test",
            AdminPassword: "SecurePassword#2026"
        ));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Rotation App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.github.com/hook/rotation", "Rotation Webhooks", 600, 15, new List<string> { "event.*" }));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // 2. Rotate Secret (Creates v2 Active, moves v1 to Rotating)
        var rotateRes = await _client.PostAsync($"/api/v1/endpoints/{ep!.Id}/secrets/rotate", null);
        rotateRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var rotated = await rotateRes.Content.ReadFromJsonAsync<RotateSecretResponse>(JsonOptions);
        rotated.Should().NotBeNull();
        rotated!.NewSecret.Should().StartWith("whsec_");

        // 3. Test Signature Generation via Endpoint API (Emits dual-signature headers during rotation)
        var testPayload = "{\"event\":\"order.created\",\"amount\":100}";
        var genRes = await _client.PostAsJsonAsync(
            $"/api/v1/endpoints/{ep.Id}/signatures/generate",
            new GenerateSignatureCommand(testPayload));
        genRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var gen = await genRes.Content.ReadFromJsonAsync<GenerateSignatureResponse>(JsonOptions);
        gen.Should().NotBeNull();
        gen!.SignatureHeader.Should().StartWith("t=");

        // 4. Verify Signature with Endpoint Verify Endpoint -> Valid
        var verifyRes = await _client.PostAsJsonAsync(
            $"/api/v1/endpoints/{ep.Id}/signatures/verify",
            new VerifySignatureCommand(testPayload, gen.SignatureHeader));
        verifyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var verify = await verifyRes.Content.ReadFromJsonAsync<VerifySignatureResponse>(JsonOptions);
        verify.Should().NotBeNull();
        verify!.IsValid.Should().BeTrue();
    }
}
