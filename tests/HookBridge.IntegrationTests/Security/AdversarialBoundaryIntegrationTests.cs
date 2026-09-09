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

namespace HookBridge.IntegrationTests.Security;

public sealed class AdversarialBoundaryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AdversarialBoundaryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterTenantAsync(string slugPrefix)
    {
        var slug = $"{slugPrefix}-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            TenantIdentifier: slug,
            TenantName: $"Security Org {slugPrefix}",
            AdminEmail: $"{slug}@sec.test",
            AdminPassword: "SecurePassword#2026"
        ));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
    }

    [Fact]
    public async Task Oversized_And_Deeply_Nested_Payload_Analysis_And_Validation()
    {
        // Arrange: Authenticate
        var token = await RegisterTenantAsync("nested-payload");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Construct 20-level nested JSON structure
        var nestedJson = "{\"level0\":{\"level1\":{\"level2\":{\"level3\":{\"level4\":{\"level5\":{\"level6\":{\"level7\":{\"level8\":{\"level9\":{\"data\":\"deep_value\",\"emoji\":\"🛡️🚀\",\"special_chars\":\"<script>alert(1)</script>\"}}}}}}}}}}}";

        // Act: Analyze payload
        var analyzeRes = await _client.PostAsJsonAsync("/api/v1/payloads/analyze", new AnalyzePayloadRequest(nestedJson));

        // Assert
        analyzeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var analyzed = await analyzeRes.Content.ReadFromJsonAsync<PayloadAnalysisResponse>(JsonOptions);
        analyzed.Should().NotBeNull();
        analyzed!.MaxDepth.Should().BeGreaterThanOrEqualTo(10);
        analyzed.IsMultibyte.Should().BeTrue();
        analyzed.InferredSchemaJson.Should().NotBeNullOrWhiteSpace();

        // Validate payload against inferred schema
        var validateRes = await _client.PostAsJsonAsync("/api/v1/payloads/validate", new ValidatePayloadSchemaRequest(
            PayloadJson: nestedJson,
            SchemaJson: analyzed.InferredSchemaJson
        ));
        validateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var validated = await validateRes.Content.ReadFromJsonAsync<PayloadSchemaValidationResponse>(JsonOptions);
        validated!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Malicious_JSONPath_Injection_Attempts_Are_Safely_Handled()
    {
        // Arrange
        var token = await RegisterTenantAsync("jsonpath-sec");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = """
        {
            "users": [
                { "name": "Admin", "role": "root" },
                { "name": "Guest", "role": "viewer" }
            ],
            "config": {
                "flag": true
            }
        }
        """;

        // Test arbitrary query with path traversal attempts
        var queries = new[]
        {
            "../../etc/passwd",
            "$(cat /etc/shadow)",
            "'; DROP TABLE Deliveries; --",
            "<script>alert(1)</script>",
            "$.users[99999].nonexistent",
            "$.*.*.*.*"
        };

        foreach (var query in queries)
        {
            var res = await _client.PostAsJsonAsync("/api/v1/payloads/jsonpath", new EvaluateJsonPathRequest(payload, query));
            // Should either be 200 with 0 matches or 400 Bad Request / Validation failure, but NEVER 500
            res.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task Cross_Tenant_Replay_And_Lineage_Tampering_Is_Strictly_Forbidden()
    {
        // 1. Tenant A creates App, Endpoint, and Delivery
        var tokenA = await RegisterTenantAsync("victim-a");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Tenant A App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.tenanta.com/hook", "Ep A", 600, 15, new List<string> { "*" }));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        var payload = JsonDocument.Parse("{\"event\":\"test\"}").RootElement;
        await _client.PostAsJsonAsync("/api/v1/events", new PublishEventCommand("test.event", payload));

        var listRes = await _client.GetAsync("/api/v1/deliveries");
        var paged = await listRes.Content.ReadFromJsonAsync<PagedList<DeliveryResponse>>(JsonOptions);
        var deliveryA = paged!.Items[0];

        // 2. Tenant B authenticates
        var tokenB = await RegisterTenantAsync("attacker-b");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        // Attempt IDOR replay on Tenant A's delivery
        var idorReplayRes = await _client.PostAsync($"/api/v1/deliveries/{deliveryA.Id}/replay", null);
        idorReplayRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Attempt IDOR lineage inspection on Tenant A's delivery
        var idorLineageRes = await _client.GetAsync($"/api/v1/deliveries/{deliveryA.Id}/lineage");
        idorLineageRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Attempt IDOR attempt recording on Tenant A's delivery
        var idorAttemptRes = await _client.PostAsJsonAsync($"/api/v1/deliveries/{deliveryA.Id}/attempts", new RecordDeliveryAttemptCommand(
            200, "{}", "{}", "{}", "{}", 10, null, DeliveryStatus.Success));
        idorAttemptRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Attempt IDOR health inspection on Tenant A's endpoint
        var idorHealthRes = await _client.GetAsync($"/api/v1/endpoints/{ep!.Id}/health");
        idorHealthRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Token_Tamper_And_Malformed_Headers_Are_Rejected()
    {
        // 1. Missing / malformed authorization header
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "malformed.jwt.token");
        var res1 = await _client.GetAsync("/api/v1/apps");
        res1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Invalid signature JWT
        var forgedJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forgedJwt);
        var res2 = await _client.GetAsync("/api/v1/apps");
        res2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Simulator_Rapid_Fire_Fault_Injection_Returns_Expected_Headers()
    {
        // 429 Rate limit should return Retry-After header
        var res429 = await _client.PostAsync("/api/v1/simulator/http/429", null);
        res429.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        res429.Headers.Contains("Retry-After").Should().BeTrue();

        // 503 Service Unavailable
        var res503 = await _client.PostAsync("/api/v1/simulator/http/503", null);
        res503.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        // Chaos endpoint should return one of standard fault codes
        var resChaos = await _client.PostAsync("/api/v1/simulator/chaos", null);
        var expectedCodes = new[] { HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway, HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout };
        expectedCodes.Should().Contain(resChaos.StatusCode);
    }
}
