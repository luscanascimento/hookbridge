using FluentAssertions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Payloads;
using HookBridge.Infrastructure.MultiTenancy;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class PayloadUseCasesTests
{
    private readonly TenantContext _tenantContext;
    private readonly Guid _tenantId;

    public PayloadUseCasesTests()
    {
        _tenantId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "test-tenant");
    }

    [Fact]
    public void AnalyzePayload_WithValidJson_ShouldReturnDetailedMetricsAndInferredSchema()
    {
        // Arrange
        var useCase = new AnalyzePayloadUseCase(_tenantContext);
        var json = """
        {
            "id": "123e4567-e89b-12d3-a456-426614174000",
            "event": "order.completed",
            "amount": 149.99,
            "isLive": true,
            "tags": ["retail", "express"],
            "customer": {
                "name": "Jane Doe",
                "email": "jane@example.com"
            },
            "notes": null
        }
        """;

        // Act
        var result = useCase.Execute(new AnalyzePayloadRequest(json));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var data = result.Value;
        data.RawByteSize.Should().BeGreaterThan(0);
        data.MinifiedByteSize.Should().BeLessThan(data.FormattedByteSize);
        data.EstimatedGzipByteSize.Should().BeGreaterThan(0);
        data.CompressionRatioPercent.Should().BeGreaterThanOrEqualTo(0);
        data.TotalKeys.Should().Be(9); // id, event, amount, isLive, tags, customer, name, email, notes
        data.MaxDepth.Should().Be(3);
        data.ArrayCount.Should().Be(1);
        data.ObjectCount.Should().Be(2); // root + customer
        data.StringCount.Should().Be(6); // id, event, 2 tags, name, email
        data.NumberCount.Should().Be(1);
        data.BooleanCount.Should().Be(1);
        data.NullCount.Should().Be(1);
        data.InferredSchemaJson.Should().Contain("\"$schema\"");
        data.InferredSchemaJson.Should().Contain("\"type\": \"object\"");
        data.InferredSchemaJson.Should().Contain("\"required\"");
    }

    [Fact]
    public void AnalyzePayload_WithMultibyteUtf8Characters_ShouldDetectNonAscii()
    {
        // Arrange
        var useCase = new AnalyzePayloadUseCase(_tenantContext);
        var json = """{"message": "Olá Mundo! 🚀 Rocket", "status": "crème brûlée"}""";

        // Act
        var result = useCase.Execute(new AnalyzePayloadRequest(json));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsMultibyte.Should().BeTrue();
        result.Value.NonAsciiCharacterCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AnalyzePayload_WithInvalidJson_ShouldReturnValidationError()
    {
        // Arrange
        var useCase = new AnalyzePayloadUseCase(_tenantContext);
        var json = "{ invalid json content }";

        // Act
        var result = useCase.Execute(new AnalyzePayloadRequest(json));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payload.InvalidJson");
    }

    [Fact]
    public void AnalyzePayload_WithoutTenant_ShouldReturnUnauthorized()
    {
        // Arrange
        var emptyTenant = new TenantContext();
        var useCase = new AnalyzePayloadUseCase(emptyTenant);

        // Act
        var result = useCase.Execute(new AnalyzePayloadRequest("{}"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.Unresolved");
    }

    [Fact]
    public void EvaluateJsonPath_WithDotAndArrayQueries_ShouldReturnMatchingNodes()
    {
        // Arrange
        var useCase = new EvaluateJsonPathUseCase(_tenantContext);
        var json = """
        {
            "event": "payment.succeeded",
            "data": {
                "items": [
                    { "sku": "ITEM-1", "price": 10.5 },
                    { "sku": "ITEM-2", "price": 25.0 }
                ]
            }
        }
        """;

        // Act 1 - Specific property
        var res1 = useCase.Execute(new EvaluateJsonPathRequest(json, "$.event"));
        res1.IsSuccess.Should().BeTrue();
        res1.Value.Matches.Should().HaveCount(1);
        res1.Value.Matches[0].Path.Should().Be("$.event");
        res1.Value.Matches[0].ValueJson.Should().Contain("payment.succeeded");

        // Act 2 - Array wildcard
        var res2 = useCase.Execute(new EvaluateJsonPathRequest(json, "$.data.items[*].sku"));
        res2.IsSuccess.Should().BeTrue();
        res2.Value.Matches.Should().HaveCount(2);
        res2.Value.Matches[0].Path.Should().Be("$.data.items[0].sku");
        res2.Value.Matches[1].Path.Should().Be("$.data.items[1].sku");

        // Act 3 - Recursive descent
        var res3 = useCase.Execute(new EvaluateJsonPathRequest(json, "$..price"));
        res3.IsSuccess.Should().BeTrue();
        res3.Value.Matches.Should().HaveCount(2);
    }

    [Fact]
    public void DiffPayloads_WithModifications_ShouldIdentifyAddedRemovedAndModified()
    {
        // Arrange
        var useCase = new DiffPayloadsUseCase(_tenantContext);
        var left = """
        {
            "id": 101,
            "status": "pending",
            "notes": "initial"
        }
        """;
        var right = """
        {
            "id": 101,
            "status": "completed",
            "deliveredAt": "2026-09-06T12:00:00Z"
        }
        """;

        // Act
        var result = useCase.Execute(new DiffPayloadsRequest(left, right));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var diff = result.Value;
        diff.HasDifferences.Should().BeTrue();
        diff.AddedCount.Should().Be(1); // deliveredAt
        diff.RemovedCount.Should().Be(1); // notes
        diff.ModifiedCount.Should().Be(1); // status
        diff.UnchangedCount.Should().Be(1); // id

        diff.Entries.Should().Contain(e => e.Path == "$.deliveredAt" && e.DiffType == "Added");
        diff.Entries.Should().Contain(e => e.Path == "$.notes" && e.DiffType == "Removed");
        diff.Entries.Should().Contain(e => e.Path == "$.status" && e.DiffType == "Modified");
    }

    [Fact]
    public void DiffPayloads_WithIdenticalPayloads_ShouldReportNoDifferences()
    {
        // Arrange
        var useCase = new DiffPayloadsUseCase(_tenantContext);
        var json = """{"user": "alice", "active": true}""";

        // Act
        var result = useCase.Execute(new DiffPayloadsRequest(json, json));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasDifferences.Should().BeFalse();
        result.Value.AddedCount.Should().Be(0);
        result.Value.RemovedCount.Should().Be(0);
        result.Value.ModifiedCount.Should().Be(0);
        result.Value.UnchangedCount.Should().Be(2);
    }

    [Fact]
    public void ValidatePayloadSchema_WithConformingPayload_ShouldPassValidation()
    {
        // Arrange
        var useCase = new ValidatePayloadSchemaUseCase(_tenantContext);
        var payload = """
        {
            "userId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
            "email": "user@hookbridge.io",
            "age": 30,
            "isAdmin": false
        }
        """;
        var schema = """
        {
            "type": "object",
            "required": ["userId", "email"],
            "properties": {
                "userId": { "type": "string", "format": "uuid" },
                "email": { "type": "string", "format": "email" },
                "age": { "type": "integer" },
                "isAdmin": { "type": "boolean" }
            }
        }
        """;

        // Act
        var result = useCase.Execute(new ValidatePayloadSchemaRequest(payload, schema));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeTrue();
        result.Value.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public void ValidatePayloadSchema_WithViolations_ShouldReturnDescriptiveErrors()
    {
        // Arrange
        var useCase = new ValidatePayloadSchemaUseCase(_tenantContext);
        var payload = """
        {
            "userId": "not-a-valid-uuid",
            "age": "thirty"
        }
        """;
        var schema = """
        {
            "type": "object",
            "required": ["userId", "email"],
            "properties": {
                "userId": { "type": "string", "format": "uuid" },
                "email": { "type": "string", "format": "email" },
                "age": { "type": "integer" }
            }
        }
        """;

        // Act
        var result = useCase.Execute(new ValidatePayloadSchemaRequest(payload, schema));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeFalse();
        result.Value.ValidationErrors.Should().Contain(e => e.Contains("Missing required property 'email'"));
        result.Value.ValidationErrors.Should().Contain(e => e.Contains("not a valid UUID"));
        result.Value.ValidationErrors.Should().Contain(e => e.Contains("Expected type 'integer'"));
    }
}
