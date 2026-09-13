using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Deliveries;
using HookBridge.Application.ControlPlane.UseCases.Endpoints;
using HookBridge.Application.ControlPlane.UseCases.Simulator;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using HookBridge.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.IntegrationTests.Chaos;

public class DistributedFailureIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly FakeEventFlowClient _fakeEventFlowClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DistributedFailureIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _fakeEventFlowClient = factory.Services.GetRequiredService<FakeEventFlowClient>();
        _fakeEventFlowClient.Reset();
    }

    public void Dispose()
    {
        _fakeEventFlowClient.Reset();
        GC.SuppressFinalize(this);
    }

    private async Task<string> RegisterAndGetTokenAsync(string slugPrefix = "chaos")
    {
        var slug = $"{slugPrefix}-{Guid.NewGuid():N}"[..16];
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            TenantIdentifier: slug,
            TenantName: "Chaos Resilience Corp",
            AdminEmail: $"{slug}@test.com",
            AdminPassword: "Password#2026"));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
    }

    [Fact]
    public async Task PublishEvent_WhenEventFlowBrokerDown_ReturnsProblemDetailsAndRecoversWhenRestored()
    {
        // 1. Arrange - Register tenant and setup endpoint
        var token = await RegisterAndGetTokenAsync("broker-down");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Order Service", "Handles ecommerce orders"));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: app!.Id,
            TargetUrl: "https://api.github.com/webhook/orders",
            Description: "Primary Webhook",
            RateLimitPerMinute: 120,
            TimeoutSeconds: 15,
            SubscribedEvents: new List<string> { "order.*" }));
        epRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Simulate Broker Outage
        _fakeEventFlowClient.ShouldFailIngest = true;
        _fakeEventFlowClient.IngestFailureError = DomainError.Failure("EventFlow.ConnectionError", "RabbitMQ Broker connection unreachable at amqp://broker:5672");

        var payload = JsonDocument.Parse("{\"orderId\":\"ord_chaos_101\",\"amount\":99.99}").RootElement;
        var publishCmd = new PublishEventCommand(
            EventType: "order.created",
            Payload: payload,
            IdempotencyKey: "idemp_chaos_01",
            Version: 1,
            CorrelationId: "corr_chaos_01");

        // 3. Act - Publish while broker down
        var failedPublishRes = await _client.PostAsJsonAsync("/api/v1/events", publishCmd);

        // Assert failure
        failedPublishRes.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var problemBody = await failedPublishRes.Content.ReadAsStringAsync();
        problemBody.Should().Contain("EventFlow.ConnectionError");

        // 4. Restore Broker
        _fakeEventFlowClient.Reset();

        // 5. Act - Publish again when broker recovered
        var restoredPublishRes = await _client.PostAsJsonAsync("/api/v1/events", publishCmd);

        // Assert recovery
        restoredPublishRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var publishSuccess = await restoredPublishRes.Content.ReadFromJsonAsync<PublishEventResponse>(JsonOptions);
        publishSuccess.Should().NotBeNull();
        publishSuccess!.Status.Should().Be("Accepted");
        publishSuccess.DeliveriesScheduled.Should().Be(1);
    }

    [Fact]
    public async Task DeliveryReplay_WhenEventFlowBrokerDown_ReturnsGracefulFailureWithoutCorruptingLineage()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("replay-down");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Billing App", "Billing"));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: app!.Id,
            TargetUrl: "https://api.github.com/webhook/billing",
            Description: "Billing Endpoint",
            RateLimitPerMinute: 60,
            TimeoutSeconds: 10,
            SubscribedEvents: new List<string> { "invoice.*" }));
        epRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = JsonDocument.Parse("{\"invoiceId\":\"inv_123\"}").RootElement;
        var publishRes = await _client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand(
            EventType: "invoice.created",
            Payload: payload));
        publishRes.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var deliveriesRes = await _client.GetAsync("/api/v1/deliveries");
        var deliveriesPaged = await deliveriesRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        deliveriesPaged.Should().NotBeNull();
        deliveriesPaged!.Items.Should().NotBeEmpty();
        var deliveryId = deliveriesPaged.Items[0].Id;

        // Simulate Broker Outage during Replay
        _fakeEventFlowClient.ShouldFailIngest = true;
        _fakeEventFlowClient.IngestFailureError = DomainError.Failure("EventFlow.ConnectionError", "Broker unavailable for replay publish");

        // Act - Single Replay
        var replayRes = await _client.PostAsJsonAsync($"/api/v1/deliveries/{deliveryId}/replay", new { reason = "Manual Disaster Recovery Replay" });

        // Assert Single Replay Failure
        replayRes.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var replayErr = await replayRes.Content.ReadAsStringAsync();
        replayErr.Should().Contain("EventFlow.ConnectionError");

        // Act - Bulk Replay with Broker Outage (should return 0 replayed since ingest fails)
        var bulkReplayRes = await _client.PostAsJsonAsync("/api/v1/deliveries/replay", new BulkReplayDeliveriesCommand(
            DeliveryIds: new List<Guid> { deliveryId },
            EndpointId: null,
            EventType: null,
            Status: null,
            FromDate: null,
            ToDate: null,
            MaxCount: 50
        ));

        bulkReplayRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var bulkResponse = await bulkReplayRes.Content.ReadFromJsonAsync<BulkReplayDeliveriesResponse>(JsonOptions);
        bulkResponse!.ReplayedCount.Should().Be(0);

        // Restore Broker & Verify Replay succeeds
        _fakeEventFlowClient.Reset();
        var replaySuccessRes = await _client.PostAsJsonAsync($"/api/v1/deliveries/{deliveryId}/replay", new { reason = "Recovered Replay" });
        replaySuccessRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify Lineage is tracked
        var lineageRes = await _client.GetAsync($"/api/v1/deliveries/{deliveryId}/lineage");
        lineageRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var lineage = await lineageRes.Content.ReadFromJsonAsync<DeliveryLineageResponse>(JsonOptions);
        lineage.Should().NotBeNull();
        lineage!.RootDeliveryId.Should().Be(deliveryId);
        lineage.LineageChain.Should().HaveCount(2); // Root + 1 replayed child
    }

    [Fact]
    public async Task DeadLetterEndpoints_WhenBrokerDown_ReturnsStructuredProblemDetails()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("dlq-down");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _fakeEventFlowClient.ShouldFailDlq = true;

        // 1. Peek DLQ
        var peekRes = await _client.GetAsync("/api/v1/dlq?count=10");
        peekRes.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var peekBody = await peekRes.Content.ReadAsStringAsync();
        peekBody.Should().Contain("EventFlow.DlqPeekFailed");

        // 2. Replay DLQ
        var replayRes = await _client.PostAsync("/api/v1/dlq/replay?maxCount=25", null);
        replayRes.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var replayBody = await replayRes.Content.ReadAsStringAsync();
        replayBody.Should().Contain("EventFlow.DlqReplayFailed");

        // 3. Purge DLQ
        var purgeRes = await _client.DeleteAsync("/api/v1/dlq");
        purgeRes.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var purgeBody = await purgeRes.Content.ReadAsStringAsync();
        purgeBody.Should().Contain("EventFlow.DlqPurgeFailed");

        // 4. Recover DLQ and verify all work
        _fakeEventFlowClient.Reset();

        var peekOk = await _client.GetAsync("/api/v1/dlq?count=5");
        peekOk.StatusCode.Should().Be(HttpStatusCode.OK);

        var replayOk = await _client.PostAsync("/api/v1/dlq/replay?maxCount=5", null);
        replayOk.StatusCode.Should().Be(HttpStatusCode.OK);

        var purgeOk = await _client.DeleteAsync("/api/v1/dlq");
        purgeOk.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FlakyEndpoint_SequentialRetryPattern_TransitionsFromFailedToSuccess()
    {
        // 1. Arrange - Setup tenant, app, and endpoint
        var token = await RegisterAndGetTokenAsync("flaky-ep");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Flaky App", "Flaky App"));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: app!.Id,
            TargetUrl: "https://api.github.com/webhook/flaky",
            Description: "Flaky Endpoint",
            RateLimitPerMinute: 120,
            TimeoutSeconds: 10,
            SubscribedEvents: new List<string> { "payment.*" }));
        var endpoint = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // 2. Create Simulator Rule for Flaky Target: 2 Failures (503) then 200 OK
        var slug = $"flaky-{Guid.NewGuid():N}"[..16];
        var ruleRes = await _client.PostAsJsonAsync("/api/v1/simulator/rules", new CreateSimulatorRuleCommand(
            Name: "Flaky 503 Retry Recovery Rule",
            Slug: slug,
            Description: "Simulates transient 503 outage recovering on 3rd attempt",
            Strategy: SimulatorStrategy.SequentialRetryPattern,
            TargetStatusCode: 503,
            SuccessStatusCode: 200,
            FailureStepCount: 2
        ));
        ruleRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var rule = await ruleRes.Content.ReadFromJsonAsync<SimulatorRuleResponse>(JsonOptions);

        // 3. Publish initial event
        var payload = JsonDocument.Parse("{\"paymentId\":\"pay_999\",\"status\":\"pending\"}").RootElement;
        var publishRes = await _client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand(
            EventType: "payment.processing",
            Payload: payload));
        publishRes.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var delivRes = await _client.GetAsync($"/api/v1/deliveries?endpointId={endpoint!.Id}");
        var delivPaged = await delivRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        var deliveryId = delivPaged!.Items[0].Id;

        // 4. Attempt 1: Receiver returns 503
        var receiverClient = _factory.CreateClient();
        var res1 = await receiverClient.PostAsync($"/api/v1/simulator/receive/{rule!.Slug}", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        res1.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var rec1 = await _client.PostAsJsonAsync($"/api/v1/deliveries/{deliveryId}/attempts", new RecordDeliveryAttemptCommand(
            HttpStatusCode: 503,
            RequestHeadersJson: "{}",
            RequestBody: "{}",
            ResponseHeadersJson: "{}",
            ResponseBody: "{\"error\":\"Service Unavailable\"}",
            ElapsedMs: 145,
            ErrorMessage: "HTTP 503 Service Unavailable",
            FinalStatus: DeliveryStatus.Failed
        ));
        rec1.StatusCode.Should().Be(HttpStatusCode.Created);

        // 5. Attempt 2: Receiver returns 503
        var res2 = await receiverClient.PostAsync($"/api/v1/simulator/receive/{rule.Slug}", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        res2.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var rec2 = await _client.PostAsJsonAsync($"/api/v1/deliveries/{deliveryId}/attempts", new RecordDeliveryAttemptCommand(
            HttpStatusCode: 503,
            RequestHeadersJson: "{}",
            RequestBody: "{}",
            ResponseHeadersJson: "{}",
            ResponseBody: "{\"error\":\"Service Unavailable\"}",
            ElapsedMs: 120,
            ErrorMessage: "HTTP 503 Service Unavailable",
            FinalStatus: DeliveryStatus.Failed
        ));
        rec2.StatusCode.Should().Be(HttpStatusCode.Created);

        // 6. Attempt 3: Receiver recovers and returns 200 OK!
        var res3 = await receiverClient.PostAsync($"/api/v1/simulator/receive/{rule.Slug}", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        res3.StatusCode.Should().Be(HttpStatusCode.OK);

        var rec3 = await _client.PostAsJsonAsync($"/api/v1/deliveries/{deliveryId}/attempts", new RecordDeliveryAttemptCommand(
            HttpStatusCode: 200,
            RequestHeadersJson: "{}",
            RequestBody: "{}",
            ResponseHeadersJson: "{}",
            ResponseBody: "{\"status\":\"ok\"}",
            ElapsedMs: 42,
            ErrorMessage: null,
            FinalStatus: DeliveryStatus.Success
        ));
        rec3.StatusCode.Should().Be(HttpStatusCode.Created);

        // 7. Inspect Delivery & Attempts Timeline
        var detailRes = await _client.GetAsync($"/api/v1/deliveries/{deliveryId}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailRes.Content.ReadFromJsonAsync<DeliveryDetailResponse>(JsonOptions);

        detail.Should().NotBeNull();
        detail!.Status.Should().Be(DeliveryStatus.Success);
        detail.Attempts.Should().HaveCount(3);
        detail.Attempts[0].HttpStatusCode.Should().Be(503);
        detail.Attempts[1].HttpStatusCode.Should().Be(503);
        detail.Attempts[2].HttpStatusCode.Should().Be(200);
    }

    [Fact]
    public async Task CircuitBreaker_FullDegradationAndRecoveryLifecycle_UnderFailureSpikes()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("circuit-lifecycle");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Circuit App", "Circuit App"));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: app!.Id,
            TargetUrl: "https://api.github.com/webhook/circuit",
            Description: "Circuit Breaker Monitored Endpoint",
            RateLimitPerMinute: 100,
            TimeoutSeconds: 10,
            SubscribedEvents: new List<string> { "circuit.*" }));
        var endpoint = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);
        var endpointId = endpoint!.Id;

        // Check Initial Health: 0 attempts -> 100% Closed
        var healthRes0 = await _client.GetAsync($"/api/v1/endpoints/{endpointId}/health");
        healthRes0.StatusCode.Should().Be(HttpStatusCode.OK);
        var h0 = await healthRes0.Content.ReadFromJsonAsync<EndpointHealthResponse>(JsonOptions);
        h0!.CircuitState.Should().Be("Closed");
        h0.HealthScorePercent.Should().Be(100);

        // Helper to create and record delivery attempt
        async Task RecordAttemptAsync(int statusCode, string? error, DeliveryStatus finalStatus)
        {
            var payload = JsonDocument.Parse("{\"test\":true}").RootElement;
            await _client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand(
                EventType: "circuit.event",
                Payload: payload));
            var listRes = await _client.GetAsync($"/api/v1/deliveries?endpointId={endpointId}");
            var list = await listRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
            var latestDeliv = list!.Items.OrderByDescending(d => d.CreatedAt).First();

            await _client.PostAsJsonAsync($"/api/v1/deliveries/{latestDeliv.Id}/attempts", new RecordDeliveryAttemptCommand(
                HttpStatusCode: statusCode,
                RequestHeadersJson: "{}",
                RequestBody: "{}",
                ResponseHeadersJson: "{}",
                ResponseBody: statusCode == 200 ? "{}" : "{\"error\":\"fail\"}",
                ElapsedMs: 50,
                ErrorMessage: error,
                FinalStatus: finalStatus
            ));
        }

        // 1. Inject 3 consecutive failures -> Circuit transitions to HalfOpen
        for (int i = 1; i <= 3; i++)
        {
            await RecordAttemptAsync(500, "Internal Server Error", DeliveryStatus.Failed);
        }

        var healthRes3 = await _client.GetAsync($"/api/v1/endpoints/{endpointId}/health");
        var h3 = await healthRes3.Content.ReadFromJsonAsync<EndpointHealthResponse>(JsonOptions);
        h3!.CircuitState.Should().Be("HalfOpen");
        h3.ConsecutiveFailures.Should().Be(3);
        h3.HealthScorePercent.Should().BeLessThan(90);

        // 2. Inject 2 more failures (total 5 consecutive) -> Circuit transitions to Open & Incidents Triggered
        for (int i = 1; i <= 2; i++)
        {
            await RecordAttemptAsync(503, "Service Unavailable", DeliveryStatus.Failed);
        }

        var healthRes5 = await _client.GetAsync($"/api/v1/endpoints/{endpointId}/health");
        var h5 = await healthRes5.Content.ReadFromJsonAsync<EndpointHealthResponse>(JsonOptions);
        h5!.CircuitState.Should().Be("Open");
        h5.ConsecutiveFailures.Should().Be(5);
        h5.HealthScorePercent.Should().BeLessThan(60);
        h5.Incidents.Should().NotBeEmpty();
        h5.Incidents.Should().Contain(inc => inc.Severity == "Critical");

        // 3. Recovery: Inject successful attempts
        for (int i = 1; i <= 3; i++)
        {
            await RecordAttemptAsync(200, null, DeliveryStatus.Success);
        }

        var healthResRecovered = await _client.GetAsync($"/api/v1/endpoints/{endpointId}/health");
        var hRecovered = await healthResRecovered.Content.ReadFromJsonAsync<EndpointHealthResponse>(JsonOptions);
        hRecovered!.ConsecutiveFailures.Should().Be(0);
        hRecovered.CircuitState.Should().Be("Closed");
    }

    [Fact]
    public async Task HighThroughput_ConcurrentFaultSimulations_ExecuteWithoutDeadlocks()
    {
        var receiverClient = _factory.CreateClient();

        // Concurrently invoke 15 fault endpoints
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(receiverClient.PostAsync("/api/v1/simulator/http/429?retryAfter=30", new StringContent("{}", System.Text.Encoding.UTF8, "application/json")));
            tasks.Add(receiverClient.PostAsync("/api/v1/simulator/timeout?delay=5&statusCode=504", new StringContent("{}", System.Text.Encoding.UTF8, "application/json")));
            tasks.Add(receiverClient.PostAsync("/api/v1/simulator/chaos?failureRate=100&failStatus=502", new StringContent("{}", System.Text.Encoding.UTF8, "application/json")));
        }

        var responses = await Task.WhenAll(tasks);
        responses.Should().HaveCount(15);

        // Verify status codes
        responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests).Should().HaveCount(5);
        responses.Where(r => r.StatusCode == HttpStatusCode.GatewayTimeout).Should().HaveCount(5);
        responses.Where(r => r.StatusCode == HttpStatusCode.BadGateway).Should().HaveCount(5);
    }

    [Fact]
    public async Task EventFlow_BrokerOutage_BlastRadius_IsContained_And_DoesNotAffect_Unrelated_Tenant()
    {
        // Arrange: Setup Tenant A and Tenant B
        var tokenA = await RegisterAndGetTokenAsync("blast-a");
        var tokenB = await RegisterAndGetTokenAsync("blast-b");

        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        // Tenant A creates app and endpoint
        var appResA = await clientA.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Tenant A App", "Desc"));
        var appA = await appResA.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);
        await clientA.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: appA!.Id,
            TargetUrl: "https://api.github.com/webhook/tenant-a",
            SubscribedEvents: new List<string> { "event.*" }));

        // Act 1: Simulate catastrophic EventFlow / RabbitMQ broker outage
        _fakeEventFlowClient.ShouldFailIngest = true;
        _fakeEventFlowClient.IngestFailureError = DomainError.Failure("EventFlow.BrokerDown", "EventFlow broker cluster unreachable at amqp://broker:5672");

        // Tenant A attempts event ingestion -> Fails gracefully with 500 and ProblemDetails
        var payload = JsonDocument.Parse("{\"test\":\"fail\"}").RootElement;
        var failRes = await clientA.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("event.test", payload));
        failRes.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var errBody = await failRes.Content.ReadAsStringAsync();
        errBody.Should().Contain("EventFlow.BrokerDown");

        // Act 2: Tenant B executes standard control plane operations simultaneously
        var appResB = await clientB.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Tenant B App", "Desc B"));
        appResB.StatusCode.Should().Be(HttpStatusCode.Created);

        var listAppsResB = await clientB.GetAsync("/api/v1/apps");
        listAppsResB.StatusCode.Should().Be(HttpStatusCode.OK);

        var meResB = await clientB.GetAsync("/api/v1/auth/me");
        meResB.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: Unrelated tenant control plane operations were completely unaffected by the Data Plane broker outage

        // Act 3: Restore broker and verify Tenant A recovers
        _fakeEventFlowClient.Reset();
        var recoverRes = await clientA.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("event.test", payload));
        recoverRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task CircuitBreaker_FailureOnOneEndpoint_DoesNotDegrade_OtherEndpoints()
    {
        // Arrange: Setup tenant with 2 separate endpoints
        var token = await RegisterAndGetTokenAsync("cb-blast");
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var appRes = await client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Telemetry Cluster", "Cluster"));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var ep1Res = await client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: app!.Id,
            TargetUrl: "https://api.github.com/webhook/ep1-fail",
            Description: "Degraded Target",
            SubscribedEvents: new List<string> { "order.*" }));
        var ep1 = await ep1Res.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        var ep2Res = await client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            ApplicationId: app.Id,
            TargetUrl: "https://api.github.com/webhook/ep2-healthy",
            Description: "Healthy Target",
            SubscribedEvents: new List<string> { "customer.*" }));
        var ep2 = await ep2Res.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // Act 1: Inject 5 consecutive failures strictly into Endpoint 1
        for (int i = 0; i < 5; i++)
        {
            var payload = JsonDocument.Parse($"{{\"i\":{i}}}").RootElement;
            await client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("order.created", payload));
            var deliveriesRes = await client.GetAsync($"/api/v1/deliveries?endpointId={ep1!.Id}");
            var list = await deliveriesRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
            var deliv = list!.Items.OrderByDescending(d => d.CreatedAt).First();

            await client.PostAsJsonAsync($"/api/v1/deliveries/{deliv.Id}/attempts", new RecordDeliveryAttemptCommand(
                HttpStatusCode: 503,
                RequestHeadersJson: "{}",
                RequestBody: "{}",
                ResponseHeadersJson: "{}",
                ResponseBody: "{\"error\":\"Service Unavailable\"}",
                ElapsedMs: 80,
                ErrorMessage: "Service Unavailable",
                FinalStatus: DeliveryStatus.Failed));
        }

        // Act 2: Dispatch a successful event and attempt to Endpoint 2
        var payload2 = JsonDocument.Parse("{\"customer\":\"vip_01\"}").RootElement;
        await client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("customer.created", payload2));
        var deliveriesRes2 = await client.GetAsync($"/api/v1/deliveries?endpointId={ep2!.Id}");
        var list2 = await deliveriesRes2.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        var deliv2 = list2!.Items.OrderByDescending(d => d.CreatedAt).First();

        await client.PostAsJsonAsync($"/api/v1/deliveries/{deliv2.Id}/attempts", new RecordDeliveryAttemptCommand(
            HttpStatusCode: 200,
            RequestHeadersJson: "{}",
            RequestBody: "{}",
            ResponseHeadersJson: "{}",
            ResponseBody: "{\"status\":\"ok\"}",
            ElapsedMs: 45,
            ErrorMessage: null,
            FinalStatus: DeliveryStatus.Success));

        // Assert 1: Endpoint 1 circuit is Open and degraded
        var healthRes1 = await client.GetAsync($"/api/v1/endpoints/{ep1!.Id}/health");
        healthRes1.StatusCode.Should().Be(HttpStatusCode.OK);
        var health1 = await healthRes1.Content.ReadFromJsonAsync<EndpointHealthResponse>(JsonOptions);
        health1!.CircuitState.Should().Be("Open");
        health1.ConsecutiveFailures.Should().Be(5);
        health1.Incidents.Should().NotBeEmpty();

        // Assert 2: Endpoint 2 remains Healthy and Closed with 100% health score and 0 consecutive failures
        var healthRes2 = await client.GetAsync($"/api/v1/endpoints/{ep2!.Id}/health");
        healthRes2.StatusCode.Should().Be(HttpStatusCode.OK);
        var health2 = await healthRes2.Content.ReadFromJsonAsync<EndpointHealthResponse>(JsonOptions);
        health2!.CircuitState.Should().Be("Closed");
        health2.ConsecutiveFailures.Should().Be(0);
        health2.Incidents.Should().BeEmpty();
        health2.HealthScorePercent.Should().Be(100);
    }
}
