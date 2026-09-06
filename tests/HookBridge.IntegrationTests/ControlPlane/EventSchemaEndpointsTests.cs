using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Enums;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class EventSchemaEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EventSchemaEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task EventSchemaEndpoints_RequireAuthorization_ShouldReturn401WhenUnauthenticated()
    {
        // Act
        var res = await _client.GetAsync("/api/v1/schemas");

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EventSchemaEndpoints_CompleteLifecycle_Create_Version_Compatibility_Drift_Docs()
    {
        // Arrange - Register and authenticate tenant A
        var slug = $"schema-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Schema Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var v1SchemaJson = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "required": ["orderId", "amount"],
            "properties": {
                "orderId": { "type": "string", "format": "uuid" },
                "amount": { "type": "number" },
                "status": { "type": "string" }
            }
        }
        """;

        // 1. Create Event Schema
        var createReq = new CreateEventSchemaRequest(
            EventType: "order.created",
            Name: "Order Created Event",
            Description: "Published whenever a customer places an order.",
            CompatibilityMode: SchemaCompatibilityMode.Backward,
            SchemaJson: v1SchemaJson,
            Version: "1.0.0",
            VersionDescription: "Initial release",
            SamplePayloadJson: """{"orderId":"11111111-2222-3333-4444-555555555555","amount":99.9,"status":"created"}""");

        var createRes = await _client.PostAsJsonAsync("/api/v1/schemas", createReq);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var schemaDetail = await createRes.Content.ReadFromJsonAsync<EventSchemaDetailResponse>(JsonOptions);
        schemaDetail.Should().NotBeNull();
        schemaDetail!.EventType.Should().Be("order.created");
        schemaDetail.Versions.Should().HaveCount(1);
        schemaDetail.ActiveVersion.Should().NotBeNull();
        schemaDetail.ActiveVersion!.Version.Should().Be("1.0.0");

        var schemaId = schemaDetail.Id;

        // 2. Duplicate EventType returns 409 Conflict
        var dupRes = await _client.PostAsJsonAsync("/api/v1/schemas", createReq);
        dupRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 3. List Schemas
        var listRes = await _client.GetAsync("/api/v1/schemas");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listRes.Content.ReadFromJsonAsync<IReadOnlyList<EventSchemaSummaryResponse>>(JsonOptions);
        list.Should().Contain(s => s.Id == schemaId && s.EventType == "order.created");

        // 4. Standalone Compatibility Check
        var breakingV2Json = """
        {
            "type": "object",
            "required": ["orderId", "amount", "mandatoryTaxNumber"],
            "properties": {
                "orderId": { "type": "string", "format": "uuid" },
                "amount": { "type": "number" },
                "status": { "type": "string" },
                "mandatoryTaxNumber": { "type": "string" }
            }
        }
        """;

        var checkRes = await _client.PostAsJsonAsync("/api/v1/schemas/compatibility/check",
            new CheckCompatibilityRequest(v1SchemaJson, breakingV2Json, SchemaCompatibilityMode.Backward));
        checkRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkResult = await checkRes.Content.ReadFromJsonAsync<CheckCompatibilityResponse>(JsonOptions);
        checkResult!.IsCompatible.Should().BeFalse();
        checkResult.BreakingChanges.Should().NotBeEmpty();

        // 5. Create Incompatible Version without override -> 400 Bad Request
        var failVersionRes = await _client.PostAsJsonAsync($"/api/v1/schemas/{schemaId}/versions",
            new CreateSchemaVersionRequest("2.0.0", breakingV2Json, "Breaking update", null, SetActive: true, ForceOverrideCompatibility: false));
        failVersionRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 6. Create Compatible Version (adding optional field)
        var compatibleV2Json = """
        {
            "type": "object",
            "required": ["orderId", "amount"],
            "properties": {
                "orderId": { "type": "string", "format": "uuid" },
                "amount": { "type": "number" },
                "status": { "type": "string" },
                "currency": { "type": "string" }
            }
        }
        """;

        var addVersionRes = await _client.PostAsJsonAsync($"/api/v1/schemas/{schemaId}/versions",
            new CreateSchemaVersionRequest("1.1.0", compatibleV2Json, "Added currency optional field", null, SetActive: true));
        addVersionRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var newVersion = await addVersionRes.Content.ReadFromJsonAsync<EventSchemaVersionResponse>(JsonOptions);
        newVersion!.Version.Should().Be("1.1.0");
        newVersion.IsActive.Should().BeTrue();

        // 7. Validate Payload against Schema
        var validPayload = """{"orderId":"11111111-2222-3333-4444-555555555555","amount":49.5,"currency":"USD"}""";
        var valRes = await _client.PostAsJsonAsync("/api/v1/schemas/validate",
            new ValidateEventPayloadRequest("order.created", null, null, validPayload));
        valRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var val = await valRes.Content.ReadFromJsonAsync<ValidateEventPayloadResponse>(JsonOptions);
        val!.IsValid.Should().BeTrue();

        // 8. Generate Documentation & Code Snippets
        var docsRes = await _client.GetAsync($"/api/v1/schemas/{schemaId}/docs");
        docsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var docs = await docsRes.Content.ReadFromJsonAsync<SchemaDocumentationResponse>(JsonOptions);
        docs.Should().NotBeNull();
        docs!.MarkdownDocs.Should().Contain("# Event Schema: `order.created`");
        docs.TypeScriptSnippet.Should().Contain("export interface OrderCreatedPayload");
        docs.CSharpSnippet.Should().Contain("public sealed record OrderCreatedPayload");

        // 9. Deprecate v1.0.0
        var v1Id = schemaDetail.ActiveVersion.Id;
        var deprecateRes = await _client.PostAsJsonAsync($"/api/v1/schemas/{schemaId}/versions/{v1Id}/deprecate", new { });
        deprecateRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 10. Multi-Tenant Isolation: Tenant B cannot access Schema A
        var slugB = $"schema-b-{Guid.NewGuid():N}"[..16];
        var regB = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slugB, "Tenant B", $"{slugB}@test.com", "Password#2026"));
        var authB = await regB.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB!.AccessToken);

        var tenantBGet = await _client.GetAsync($"/api/v1/schemas/{schemaId}");
        tenantBGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
