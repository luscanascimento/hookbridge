using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.Services;
using HookBridge.Application.ControlPlane.UseCases.Schemas;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class EventSchemaUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _tenantId;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISchemaCompatibilityChecker _compatibilityChecker;
    private readonly ISchemaCodeGenerator _codeGenerator;

    public EventSchemaUseCasesTests()
    {
        _tenantId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "test-tenant");

        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(databaseName: $"HookBridge_SchemaTests_{Guid.NewGuid()}")
            .Options;

        _dateTimeProvider = new HookBridge.Application.Common.DateTimeProvider();
        _dbContext = new HookBridgeDbContext(options, _tenantContext, _dateTimeProvider);
        _currentUser = new TestCurrentUser(Guid.NewGuid(), "dev@test.com", UserRole.Developer);
        _compatibilityChecker = new SchemaCompatibilityChecker();
        _codeGenerator = new SchemaCodeGenerator();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; }
        public string? Email { get; }
        public UserRole? Role { get; }
        public bool IsAuthenticated => true;

        public TestCurrentUser(Guid userId, string email, UserRole role)
        {
            UserId = userId;
            Email = email;
            Role = role;
        }
    }

    [Fact]
    public async Task CreateEventSchema_WithValidData_ShouldCreateSchemaAndInitialVersion()
    {
        // Arrange
        var useCase = new CreateEventSchemaUseCase(_dbContext, _tenantContext, _currentUser, _dateTimeProvider);
        var schemaJson = """{"type":"object","required":["id"],"properties":{"id":{"type":"string"}}}""";
        var request = new CreateEventSchemaRequest(
            "order.created",
            "Order Created",
            "Fired on order creation",
            SchemaCompatibilityMode.Backward,
            schemaJson,
            "1.0.0",
            "Initial release",
            """{"id":"123"}""");

        // Act
        var result = await useCase.ExecuteAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;
        schema.EventType.Should().Be("order.created");
        schema.Name.Should().Be("Order Created");
        schema.Versions.Should().HaveCount(1);
        schema.ActiveVersion.Should().NotBeNull();
        schema.ActiveVersion!.Version.Should().Be("1.0.0");
        schema.ActiveVersion.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateSchemaVersion_WithBreakingChangeUnderBackwardMode_ShouldFailWithoutForce()
    {
        // Arrange
        var createUseCase = new CreateEventSchemaUseCase(_dbContext, _tenantContext, _currentUser, _dateTimeProvider);
        var v1Json = """{"type":"object","required":["id"],"properties":{"id":{"type":"string"}}}""";
        var initResult = await createUseCase.ExecuteAsync(new CreateEventSchemaRequest(
            "order.created", "Order Created", null, SchemaCompatibilityMode.Backward, v1Json, "1.0.0", null, null));
        var schemaId = initResult.Value.Id;

        var versionUseCase = new CreateSchemaVersionUseCase(_dbContext, _tenantContext, _currentUser, _dateTimeProvider, _compatibilityChecker);
        var breakingV2Json = """{"type":"object","required":["id","taxId"],"properties":{"id":{"type":"string"},"taxId":{"type":"string"}}}""";

        // Act
        var result = await versionUseCase.ExecuteAsync(schemaId, new CreateSchemaVersionRequest(
            "2.0.0", breakingV2Json, "Breaking addition of taxId", null, SetActive: true, ForceOverrideCompatibility: false));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Schema.Incompatible");
    }

    [Fact]
    public async Task CreateSchemaVersion_WithForceOverride_ShouldSucceed()
    {
        // Arrange
        var createUseCase = new CreateEventSchemaUseCase(_dbContext, _tenantContext, _currentUser, _dateTimeProvider);
        var v1Json = """{"type":"object","required":["id"],"properties":{"id":{"type":"string"}}}""";
        var initResult = await createUseCase.ExecuteAsync(new CreateEventSchemaRequest(
            "order.created", "Order Created", null, SchemaCompatibilityMode.Backward, v1Json, "1.0.0", null, null));
        var schemaId = initResult.Value.Id;

        var versionUseCase = new CreateSchemaVersionUseCase(_dbContext, _tenantContext, _currentUser, _dateTimeProvider, _compatibilityChecker);
        var breakingV2Json = """{"type":"object","required":["id","taxId"],"properties":{"id":{"type":"string"},"taxId":{"type":"string"}}}""";

        // Act
        var result = await versionUseCase.ExecuteAsync(schemaId, new CreateSchemaVersionRequest(
            "2.0.0", breakingV2Json, "Breaking addition of taxId", null, SetActive: true, ForceOverrideCompatibility: true));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("2.0.0");
        result.Value.VersionNumber.Should().Be(2);
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateAndDeprecateVersion_ShouldUpdateVersionLifecycle()
    {
        // Arrange
        var createUseCase = new CreateEventSchemaUseCase(_dbContext, _tenantContext, _currentUser, _dateTimeProvider);
        var schemaJson = """{"type":"object","properties":{"id":{"type":"string"}}}""";
        var init = await createUseCase.ExecuteAsync(new CreateEventSchemaRequest(
            "user.created", "User Created", null, SchemaCompatibilityMode.None, schemaJson, "1.0.0", null, null));
        var schemaId = init.Value.Id;
        var v1Id = init.Value.ActiveVersion!.Id;

        var versionUseCase = new CreateSchemaVersionUseCase(_dbContext, _tenantContext, _currentUser, _dateTimeProvider, _compatibilityChecker);
        var v2 = await versionUseCase.ExecuteAsync(schemaId, new CreateSchemaVersionRequest(
            "1.1.0", schemaJson, "Non-breaking update", null, SetActive: false));
        var v2Id = v2.Value.Id;

        var activateUseCase = new ActivateSchemaVersionUseCase(_dbContext, _tenantContext, _currentUser, _dateTimeProvider);
        var deprecateUseCase = new DeprecateSchemaVersionUseCase(_dbContext, _tenantContext, _currentUser, _dateTimeProvider);

        // Act 1 - Activate v2
        var actRes = await activateUseCase.ExecuteAsync(schemaId, v2Id);
        actRes.IsSuccess.Should().BeTrue();
        actRes.Value.IsActive.Should().BeTrue();

        // Act 2 - Deprecate v1
        var depRes = await deprecateUseCase.ExecuteAsync(schemaId, v1Id);
        depRes.IsSuccess.Should().BeTrue();
        depRes.Value.IsDeprecated.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateSchemaDocsUseCase_ShouldProduceCompleteDocumentation()
    {
        // Arrange
        var createUseCase = new CreateEventSchemaUseCase(_dbContext, _tenantContext, _currentUser, _dateTimeProvider);
        var schemaJson = """
        {
            "type": "object",
            "required": ["orderId", "amount"],
            "properties": {
                "orderId": { "type": "string", "format": "uuid" },
                "amount": { "type": "number" }
            }
        }
        """;
        var init = await createUseCase.ExecuteAsync(new CreateEventSchemaRequest(
            "order.created", "Order Created", "Dispatched when order is placed", SchemaCompatibilityMode.Backward, schemaJson, "1.0.0", null, null));

        var docsUseCase = new GenerateSchemaDocsUseCase(_dbContext, _tenantContext, _codeGenerator);

        // Act
        var result = await docsUseCase.ExecuteAsync(init.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var docs = result.Value;
        docs.EventType.Should().Be("order.created");
        docs.MarkdownDocs.Should().Contain("# Event Schema: `order.created`");
        docs.TypeScriptSnippet.Should().Contain("export interface OrderCreatedPayload");
        docs.CSharpSnippet.Should().Contain("public sealed record OrderCreatedPayload");
    }
}
