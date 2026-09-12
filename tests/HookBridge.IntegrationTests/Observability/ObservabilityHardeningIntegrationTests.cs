using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.IntegrationTests.Fixtures;
using Xunit;

namespace HookBridge.IntegrationTests.Observability;

public class ObservabilityHardeningIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ObservabilityHardeningIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid TenantId)> RegisterTenantAsync(string prefix)
    {
        var slug = $"{prefix}-{Guid.NewGuid():N}"[..16];
        var email = $"admin@{slug}.test";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, $"Tenant {prefix}", email, "Password#2026"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return (auth!.AccessToken, auth.User.TenantId);
    }

    [Fact]
    public async Task ResponseHeaders_MustPropagateTraceIdAndCorrelationId()
    {
        var customCorrelationId = $"corr_{Guid.NewGuid():N}"[..18];

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", customCorrelationId);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("X-Correlation-Id").Should().BeTrue();
        response.Headers.GetValues("X-Correlation-Id").First().Should().Be(customCorrelationId);

        response.Headers.Contains("X-Trace-Id").Should().BeTrue();
        response.Headers.GetValues("X-Trace-Id").First().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AuditTrail_MustCaptureTraceIdAndRedactSensitiveData()
    {
        // 1. Register a new tenant
        var (token, tenantId) = await RegisterTenantAsync("obs-audit");

        // 2. Fetch audit logs
        using var auditRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/audit-logs?pageSize=10");
        auditRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var auditResponse = await _client.SendAsync(auditRequest);
        auditResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedLogs = await auditResponse.Content.ReadFromJsonAsync<PagedList<AuditEntryResponse>>(JsonOptions);
        pagedLogs.Should().NotBeNull();
        pagedLogs!.Items.Should().NotBeEmpty();

        var registrationAudit = pagedLogs.Items.FirstOrDefault(a => a.Action == "Tenant.Registered");
        registrationAudit.Should().NotBeNull();

        // Trace ID must be populated
        registrationAudit!.TraceId.Should().NotBeNullOrWhiteSpace();

        // Password must NEVER be present in audit details
        registrationAudit.DetailsJson.Should().NotContain("Password#2026");

        // Admin email must be masked in audit entry
        registrationAudit.DetailsJson.Should().Contain("a***n@");
    }

    [Fact]
    public async Task WebhookSecretRotation_AuditTrail_MustNotLeakPlainSecret()
    {
        var (token, _) = await RegisterTenantAsync("obs-sec");

        // 1. Create an Application
        using var createAppReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/apps")
        {
            Content = JsonContent.Create(new CreateApplicationCommand("ObsApp", "Test App"))
        };
        createAppReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createAppRes = await _client.SendAsync(createAppReq);
        createAppRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var appDto = await createAppRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        // 2. Create an Endpoint
        using var createEndpointReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/endpoints")
        {
            Content = JsonContent.Create(new CreateEndpointCommand(
                appDto!.Id,
                "https://example.com/webhook",
                "Obs Endpoint",
                100,
                30,
                new List<string> { "order.*" }))
        };
        createEndpointReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createEndpointRes = await _client.SendAsync(createEndpointReq);
        createEndpointRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var endpointDto = await createEndpointRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // 3. Rotate Secret
        using var rotateReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/endpoints/{endpointDto!.Id}/secrets/rotate");
        rotateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var rotateRes = await _client.SendAsync(rotateReq);
        rotateRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var rotatedDto = await rotateRes.Content.ReadFromJsonAsync<RotateSecretResponse>(JsonOptions);
        rotatedDto.Should().NotBeNull();
        var plainSecret = rotatedDto!.NewSecret;
        plainSecret.Should().StartWith("whsec_");

        // 4. Inspect Audit Logs for Secret Rotation
        using var auditRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/audit-logs?resourceType=WebhookSecret");
        auditRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var auditResponse = await _client.SendAsync(auditRequest);
        auditResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedLogs = await auditResponse.Content.ReadFromJsonAsync<PagedList<AuditEntryResponse>>(JsonOptions);
        pagedLogs.Should().NotBeNull();

        var rotateAudit = pagedLogs!.Items.FirstOrDefault(a => a.Action == "WebhookSecret.Rotated");
        rotateAudit.Should().NotBeNull();

        // Must capture valid TraceId
        rotateAudit!.TraceId.Should().NotBeNullOrWhiteSpace();

        // Must NEVER leak the plain secret in audit details
        rotateAudit.DetailsJson.Should().NotContain(plainSecret);

        // Must contain safe metadata (prefix and version)
        rotateAudit.DetailsJson.Should().Contain(rotatedDto.SecretPrefix);
        rotateAudit.DetailsJson.Should().Contain("\"Version\":2");
    }
}
