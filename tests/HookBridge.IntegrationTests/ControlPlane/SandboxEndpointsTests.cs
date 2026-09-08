using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Sandbox;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class SandboxEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SandboxEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync(string? slugPrefix = null)
    {
        var slug = $"{slugPrefix ?? "sb"}-{Guid.NewGuid():N}"[..16];
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Sandbox Org", $"{slug}@test.com", "Password#2026"));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
    }

    [Fact]
    public async Task CreateSandbox_ShouldReturnSandbox_WithReceiverUrl()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("sbcreate");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var command = new CreateSandboxCommand(
            Name: "Test Webhook Box",
            CustomSlug: null,
            DefaultStatusCode: 200,
            DefaultBody: "{\"message\":\"received\"}",
            DefaultContentType: "application/json",
            DefaultDelayMs: 50,
            TtlHours: 12
        );

        // Act
        var res = await _client.PostAsJsonAsync("/api/v1/sandboxes", command);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await res.Content.ReadFromJsonAsync<WebhookSandboxResponse>(JsonOptions);
        created.Should().NotBeNull();
        created!.Name.Should().Be("Test Webhook Box");
        created.Slug.Should().NotBeNullOrWhiteSpace();
        created.ReceiverUrl.Should().Contain($"/api/v1/sandbox/receiver/{created.Slug}");
        created.DefaultResponseStatusCode.Should().Be(200);
        created.DefaultResponseBody.Should().Be("{\"message\":\"received\"}");
    }

    [Fact]
    public async Task ReceiveSandboxWebhook_PublicEndpoint_ShouldCaptureAndSimulateResponse()
    {
        // Arrange - Create Sandbox
        var token = await RegisterAndGetTokenAsync("sbrecv");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var slug = $"stripe-test-{Guid.NewGuid():N}"[..18];
        var createRes = await _client.PostAsJsonAsync("/api/v1/sandboxes", new CreateSandboxCommand(
            Name: "Stripe Webhook Sandbox",
            CustomSlug: slug,
            DefaultStatusCode: 202,
            DefaultBody: "{\"accepted\":true}",
            DefaultContentType: "application/json",
            DefaultDelayMs: 0,
            TtlHours: null
        ));
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var sandbox = await createRes.Content.ReadFromJsonAsync<WebhookSandboxResponse>(JsonOptions);

        // Act - Invoke Public Receiver without Auth
        var receiverClient = _factory.CreateClient();
        var webhookContent = new StringContent("{\"event\":\"customer.subscription.created\",\"id\":\"sub_123\"}", System.Text.Encoding.UTF8, "application/json");
        webhookContent.Headers.Add("X-Webhook-Signature", "sig_xyz_test");

        var receiverRes = await receiverClient.PostAsync($"/api/v1/sandbox/receiver/{sandbox!.Slug}?account=acct_001", webhookContent);

        // Assert Receiver Response
        receiverRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var simBody = await receiverRes.Content.ReadAsStringAsync();
        simBody.Should().Be("{\"accepted\":true}");

        // Act - Fetch Captured Requests as Tenant
        var requestsRes = await _client.GetAsync($"/api/v1/sandboxes/{sandbox.Id}/requests");
        requestsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await requestsRes.Content.ReadFromJsonAsync<PagedSandboxRequestsResponse>(JsonOptions);

        // Assert Captured Request
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().Be(1);
        var captured = paged.Items[0];
        captured.HttpMethod.Should().Be("POST");
        captured.ResponseStatusCode.Should().Be(202);

        // Fetch Detail
        var detailRes = await _client.GetAsync($"/api/v1/sandboxes/{sandbox.Id}/requests/{captured.Id}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailRes.Content.ReadFromJsonAsync<SandboxRequestResponse>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.HeadersJson.Should().Contain("X-Webhook-Signature");
        detail.QueryString.Should().Be("?account=acct_001");
        detail.Body.Should().Contain("customer.subscription.created");
    }

    [Fact]
    public async Task UpdateSandboxConfig_And_ClearRequests_ShouldSucceed()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("sbupdate");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var slug = $"update-box-{Guid.NewGuid():N}"[..18];
        var createRes = await _client.PostAsJsonAsync("/api/v1/sandboxes", new CreateSandboxCommand(
            Name: "Initial Box",
            CustomSlug: slug,
            DefaultStatusCode: 200,
            DefaultBody: "ok",
            DefaultContentType: "text/plain",
            DefaultDelayMs: 0,
            TtlHours: null
        ));
        var sandbox = await createRes.Content.ReadFromJsonAsync<WebhookSandboxResponse>(JsonOptions);

        // Send a request to capture
        var receiverClient = _factory.CreateClient();
        await receiverClient.PostAsync($"/api/v1/sandbox/receiver/{sandbox!.Slug}", new StringContent("{\"data\":1}", System.Text.Encoding.UTF8, "application/json"));

        // Update Config to return 500 error
        var updateRes = await _client.PutAsJsonAsync($"/api/v1/sandboxes/{sandbox.Id}", new UpdateSandboxConfigCommand(
            Name: "Updated Box",
            DefaultStatusCode: 500,
            DefaultBody: "{\"error\":\"simulated gateway failure\"}",
            DefaultContentType: "application/json",
            DefaultDelayMs: 20,
            IsActive: true
        ));
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify receiver now returns 500
        var simRes = await receiverClient.PostAsync($"/api/v1/sandbox/receiver/{sandbox.Slug}", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        simRes.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        // Clear requests
        var clearRes = await _client.DeleteAsync($"/api/v1/sandboxes/{sandbox.Id}/requests");
        clearRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listRes = await _client.GetAsync($"/api/v1/sandboxes/{sandbox.Id}/requests");
        var listPaged = await listRes.Content.ReadFromJsonAsync<PagedSandboxRequestsResponse>(JsonOptions);
        listPaged!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteSandbox_ShouldReturnNoContent_And404OnReceiver()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("sbdel");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var slug = $"del-box-{Guid.NewGuid():N}"[..18];
        var createRes = await _client.PostAsJsonAsync("/api/v1/sandboxes", new CreateSandboxCommand(
            Name: "Delete Me",
            CustomSlug: slug,
            DefaultStatusCode: 200,
            DefaultBody: null,
            DefaultContentType: null,
            DefaultDelayMs: 0,
            TtlHours: null
        ));
        var sandbox = await createRes.Content.ReadFromJsonAsync<WebhookSandboxResponse>(JsonOptions);

        // Act - Delete Sandbox
        var delRes = await _client.DeleteAsync($"/api/v1/sandboxes/{sandbox!.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - Receiver gives 404
        var receiverClient = _factory.CreateClient();
        var recvRes = await receiverClient.PostAsync($"/api/v1/sandbox/receiver/{sandbox.Slug}", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        recvRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
