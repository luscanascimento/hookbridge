using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class PayloadEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PayloadEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PayloadEndpoints_RequireAuthorization_ShouldReturn401WhenUnauthenticated()
    {
        // Act
        var res = await _client.PostAsJsonAsync("/api/v1/payloads/analyze", new AnalyzePayloadRequest("{}"));

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PayloadEndpoints_CompleteLifecycle_Analyze_JsonPath_Diff_Validate()
    {
        // Arrange - Register and authenticate
        var slug = $"payload-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Payload Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var samplePayload = """
        {
            "id": "evt_998877",
            "type": "charge.captured",
            "livemode": false,
            "data": {
                "object": {
                    "id": "ch_1234567890",
                    "amount": 2500,
                    "currency": "usd",
                    "status": "succeeded"
                }
            },
            "recipients": ["ops@company.com", "billing@company.com"]
        }
        """;

        // 1. Analyze Payload
        var analyzeRes = await _client.PostAsJsonAsync("/api/v1/payloads/analyze", new AnalyzePayloadRequest(samplePayload));
        analyzeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var analysis = await analyzeRes.Content.ReadFromJsonAsync<PayloadAnalysisResponse>(JsonOptions);
        analysis.Should().NotBeNull();
        analysis!.RawByteSize.Should().BeGreaterThan(0);
        analysis.ObjectCount.Should().Be(3); // root + data + data.object
        analysis.ArrayCount.Should().Be(1); // recipients
        analysis.TotalKeys.Should().Be(10);
        analysis.InferredSchemaJson.Should().Contain("InferredWebhookPayloadSchema");

        // 2. Evaluate JSONPath
        var jsonPathRes = await _client.PostAsJsonAsync("/api/v1/payloads/jsonpath", new EvaluateJsonPathRequest(samplePayload, "$.data.object.amount"));
        jsonPathRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonPathResult = await jsonPathRes.Content.ReadFromJsonAsync<JsonPathEvaluationResponse>(JsonOptions);
        jsonPathResult.Should().NotBeNull();
        jsonPathResult!.IsValid.Should().BeTrue();
        jsonPathResult.MatchCount.Should().Be(1);
        jsonPathResult.Matches[0].Path.Should().Be("$.data.object.amount");
        jsonPathResult.Matches[0].ValueJson.Should().Be("2500");

        // 3. Diff Payloads
        var modifiedPayload = """
        {
            "id": "evt_998877",
            "type": "charge.refunded",
            "livemode": false,
            "data": {
                "object": {
                    "id": "ch_1234567890",
                    "amount": 2500,
                    "currency": "usd",
                    "status": "refunded",
                    "refundReason": "duplicate"
                }
            },
            "recipients": ["ops@company.com"]
        }
        """;
        var diffRes = await _client.PostAsJsonAsync("/api/v1/payloads/diff", new DiffPayloadsRequest(samplePayload, modifiedPayload));
        diffRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var diff = await diffRes.Content.ReadFromJsonAsync<PayloadDiffResponse>(JsonOptions);
        diff.Should().NotBeNull();
        diff!.HasDifferences.Should().BeTrue();
        diff.AddedCount.Should().Be(1); // refundReason
        diff.RemovedCount.Should().Be(1); // recipients[1]
        diff.ModifiedCount.Should().Be(2); // type, status

        // 4. Validate Schema
        var schema = """
        {
            "type": "object",
            "required": ["id", "type", "data"],
            "properties": {
                "id": { "type": "string" },
                "type": { "type": "string" },
                "livemode": { "type": "boolean" },
                "data": { "type": "object" }
            }
        }
        """;
        var validateRes = await _client.PostAsJsonAsync("/api/v1/payloads/validate", new ValidatePayloadSchemaRequest(samplePayload, schema));
        validateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var validation = await validateRes.Content.ReadFromJsonAsync<PayloadSchemaValidationResponse>(JsonOptions);
        validation.Should().NotBeNull();
        validation!.IsValid.Should().BeTrue();
        validation.ValidationErrors.Should().BeEmpty();
    }
}
