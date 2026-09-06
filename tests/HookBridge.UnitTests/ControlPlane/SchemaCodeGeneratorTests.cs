using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.ControlPlane.Services;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class SchemaCodeGeneratorTests
{
    private readonly SchemaCodeGenerator _generator = new();

    private const string SampleSchema = """
    {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "title": "OrderCreated",
        "type": "object",
        "required": ["orderId", "amount", "customerEmail"],
        "properties": {
            "orderId": { "type": "string", "format": "uuid", "description": "Unique order identifier" },
            "amount": { "type": "number", "description": "Total order amount" },
            "customerEmail": { "type": "string", "format": "email", "description": "Customer email" },
            "status": { "type": "string", "enum": ["pending", "completed", "cancelled"] },
            "items": {
                "type": "array",
                "items": { "type": "string" }
            }
        }
    }
    """;

    [Fact]
    public void GenerateTypeScript_ShouldGenerateValidInterfaceWithProperTypes()
    {
        // Act
        var ts = _generator.GenerateTypeScript("order.created", SampleSchema);

        // Assert
        ts.Should().Contain("export interface OrderCreatedPayload {");
        ts.Should().Contain("orderId: string /* UUID */;");
        ts.Should().Contain("amount: number;");
        ts.Should().Contain("customerEmail: string /* Email */;");
        ts.Should().Contain("status?: 'pending' | 'completed' | 'cancelled';");
        ts.Should().Contain("items?: string[];");
    }

    [Fact]
    public void GenerateCSharp_ShouldGenerateValidRecordWithJsonPropertyNames()
    {
        // Act
        var cs = _generator.GenerateCSharp("order.created", SampleSchema);

        // Assert
        cs.Should().Contain("public sealed record OrderCreatedPayload(");
        cs.Should().Contain("[property: JsonPropertyName(\"orderId\")] Guid OrderId,");
        cs.Should().Contain("[property: JsonPropertyName(\"amount\")] decimal Amount,");
        cs.Should().Contain("[property: JsonPropertyName(\"customerEmail\")] string CustomerEmail,");
        cs.Should().Contain("[property: JsonPropertyName(\"status\")] string? Status,");
        cs.Should().Contain("[property: JsonPropertyName(\"items\")] IReadOnlyList<string>? Items);");
    }

    [Fact]
    public void GenerateMarkdownDocs_ShouldGenerateTableAndExample()
    {
        // Act
        var md = _generator.GenerateMarkdownDocs("order.created", "Order Created", "1.0.0", SampleSchema, "Dispatched when an order is created.");

        // Assert
        md.Should().Contain("# Event Schema: `order.created`");
        md.Should().Contain("- **Version:** `1.0.0`");
        md.Should().Contain("| Field | Type | Required | Format / Enum | Description |");
        md.Should().Contain("| `orderId` | `string` | ✅ Yes | `uuid` | Unique order identifier |");
        md.Should().Contain("```json");
    }

    [Fact]
    public void GenerateSamplePayload_ShouldProduceValidJsonMatchingSchema()
    {
        // Act
        var sampleJson = _generator.GenerateSamplePayload(SampleSchema);

        // Assert
        sampleJson.Should().NotBeNullOrWhiteSpace();
        using var doc = JsonDocument.Parse(sampleJson);
        var root = doc.RootElement;
        root.TryGetProperty("orderId", out var idProp).Should().BeTrue();
        idProp.GetString().Should().NotBeNullOrWhiteSpace();
        root.TryGetProperty("amount", out var amountProp).Should().BeTrue();
        amountProp.GetDouble().Should().BeGreaterThan(0);
        root.TryGetProperty("customerEmail", out var emailProp).Should().BeTrue();
        emailProp.GetString().Should().Contain("@");
    }
}
