using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.UseCases.Sandbox;
using HookBridge.Domain.Entities;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class SandboxUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly DateTimeProvider _dt;
    private readonly Guid _tenantId;

    public SandboxUseCasesTests()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "test-sandbox-tenant");

        _db = new HookBridgeDbContext(options, _tenantContext);
        _dt = new DateTimeProvider();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task CreateSandbox_ShouldCreateSandbox_WithConfiguredDefaultsAndReceiverUrl()
    {
        // Arrange
        var useCase = new CreateSandboxUseCase(_db, _tenantContext, _dt);
        var command = new CreateSandboxCommand(
            Name: "Payment Gateway Webhook Sandbox",
            CustomSlug: "payment-gw-test",
            DefaultStatusCode: 202,
            DefaultBody: "{\"received\": true}",
            DefaultContentType: "application/json",
            DefaultDelayMs: 150,
            TtlHours: 24
        );

        // Act
        var result = await useCase.ExecuteAsync(command, "https://api.hookbridge.io");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Payment Gateway Webhook Sandbox");
        result.Value.Slug.Should().Be("sb_payment-gw-test");
        result.Value.ReceiverUrl.Should().Be("https://api.hookbridge.io/api/v1/sandbox/receiver/sb_payment-gw-test");
        result.Value.DefaultResponseStatusCode.Should().Be(202);
        result.Value.DefaultResponseBody.Should().Be("{\"received\": true}");
        result.Value.DefaultResponseContentType.Should().Be("application/json");
        result.Value.DefaultResponseDelayMs.Should().Be(150);
        result.Value.IsActive.Should().BeTrue();
        result.Value.ExpiresAt.Should().NotBeNull();
        result.Value.TotalRequestsCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateSandbox_DuplicateSlug_ShouldReturnConflict()
    {
        // Arrange
        var useCase = new CreateSandboxUseCase(_db, _tenantContext, _dt);
        var command = new CreateSandboxCommand(
            Name: "Sandbox 1",
            CustomSlug: "duplicate-slug",
            DefaultStatusCode: 200,
            DefaultBody: null,
            DefaultContentType: null,
            DefaultDelayMs: 0,
            TtlHours: null
        );

        var first = await useCase.ExecuteAsync(command);
        first.IsSuccess.Should().BeTrue();

        // Act
        var second = await useCase.ExecuteAsync(command);

        // Assert
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("WebhookSandbox.SlugTaken");
    }

    [Fact]
    public async Task ProcessSandboxRequest_ShouldCaptureIncomingRequest_AndReturnSimulatedResponse()
    {
        // Arrange
        var createUseCase = new CreateSandboxUseCase(_db, _tenantContext, _dt);
        var createResult = await createUseCase.ExecuteAsync(new CreateSandboxCommand(
            Name: "Stripe Simulator",
            CustomSlug: "stripe-sim",
            DefaultStatusCode: 200,
            DefaultBody: "{\"status\":\"ok\"}",
            DefaultContentType: "application/json",
            DefaultDelayMs: 10,
            TtlHours: null
        ));
        createResult.IsSuccess.Should().BeTrue();

        var processUseCase = new ProcessSandboxRequestUseCase(_db, _dt);

        // Act
        var processResult = await processUseCase.ExecuteAsync(
            slug: createResult.Value.Slug,
            httpMethod: "POST",
            path: $"/api/v1/sandbox/receiver/{createResult.Value.Slug}",
            queryString: "?event=charge.succeeded",
            headersJson: "{\"X-Signature\":\"sig123\",\"User-Agent\":\"Stripe/1.0\"}",
            body: "{\"id\":\"evt_123\",\"type\":\"charge.succeeded\"}",
            contentType: "application/json",
            contentLength: 42,
            clientIp: "192.168.1.100"
        );

        // Assert
        processResult.IsSuccess.Should().BeTrue();
        processResult.Value.StatusCode.Should().Be(200);
        processResult.Value.Body.Should().Be("{\"status\":\"ok\"}");
        processResult.Value.ContentType.Should().Be("application/json");

        // Verify request stored in DB
        var requests = await _db.SandboxRequests
            .Where(r => r.SandboxId == createResult.Value.Id)
            .ToListAsync();

        requests.Should().HaveCount(1);
        requests[0].HttpMethod.Should().Be("POST");
        requests[0].QueryString.Should().Be("?event=charge.succeeded");
        requests[0].HeadersJson.Should().Contain("X-Signature");
        requests[0].Body.Should().Be("{\"id\":\"evt_123\",\"type\":\"charge.succeeded\"}");
        requests[0].ClientIp.Should().Be("192.168.1.100");
        requests[0].ResponseStatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ProcessSandboxRequest_InactiveSandbox_ShouldReturnConflict()
    {
        // Arrange
        var createUseCase = new CreateSandboxUseCase(_db, _tenantContext, _dt);
        var createResult = await createUseCase.ExecuteAsync(new CreateSandboxCommand("Inactive Sandbox", "inactive-box", 200, null, null, 0, null));

        var updateUseCase = new UpdateSandboxConfigUseCase(_db, _tenantContext, _dt);
        await updateUseCase.ExecuteAsync(createResult.Value.Id, new UpdateSandboxConfigCommand(
            Name: "Inactive Sandbox",
            DefaultStatusCode: 200,
            DefaultBody: null,
            DefaultContentType: "application/json",
            DefaultDelayMs: 0,
            IsActive: false
        ));

        var processUseCase = new ProcessSandboxRequestUseCase(_db, _dt);

        // Act
        var result = await processUseCase.ExecuteAsync(
            slug: createResult.Value.Slug,
            httpMethod: "POST",
            path: $"/api/v1/sandbox/receiver/{createResult.Value.Slug}",
            queryString: null,
            headersJson: "{}",
            body: "test",
            contentType: "text/plain",
            contentLength: 4,
            clientIp: "127.0.0.1"
        );

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("WebhookSandbox.Inactive");
    }

    [Fact]
    public async Task GetSandboxRequests_FilterAndPagination_ShouldWork()
    {
        // Arrange
        var createUseCase = new CreateSandboxUseCase(_db, _tenantContext, _dt);
        var createResult = await createUseCase.ExecuteAsync(new CreateSandboxCommand("Search Box", "search-box", 200, null, null, 0, null));
        var sandboxId = createResult.Value.Id;
        var slug = createResult.Value.Slug;

        var processUseCase = new ProcessSandboxRequestUseCase(_db, _dt);
        await processUseCase.ExecuteAsync(slug, "POST", $"/api/v1/sandbox/receiver/{slug}/orders", null, "{}", "{\"orderId\": 1001}", "application/json", 16, "127.0.0.1");
        await processUseCase.ExecuteAsync(slug, "GET", $"/api/v1/sandbox/receiver/{slug}/ping", null, "{}", null, null, 0, "127.0.0.1");
        await processUseCase.ExecuteAsync(slug, "POST", $"/api/v1/sandbox/receiver/{slug}/customers", null, "{}", "{\"customer\": \"Alice\"}", "application/json", 21, "127.0.0.1");

        var getRequestsUseCase = new GetSandboxRequestsUseCase(_db, _tenantContext);

        // Act - filter by POST
        var postResult = await getRequestsUseCase.ExecuteAsync(sandboxId, method: "POST", page: 1, pageSize: 10);

        // Assert
        postResult.IsSuccess.Should().BeTrue();
        postResult.Value.TotalCount.Should().Be(2);
        postResult.Value.Items.Should().HaveCount(2);

        // Act - search by "Alice"
        var searchResult = await getRequestsUseCase.ExecuteAsync(sandboxId, search: "Alice", page: 1, pageSize: 10);
        searchResult.IsSuccess.Should().BeTrue();
        searchResult.Value.TotalCount.Should().Be(1);
        searchResult.Value.Items[0].Path.Should().Contain("customers");
    }

    [Fact]
    public async Task ClearSandboxRequests_ShouldRemoveAllCapturedRequests()
    {
        // Arrange
        var createUseCase = new CreateSandboxUseCase(_db, _tenantContext, _dt);
        var createResult = await createUseCase.ExecuteAsync(new CreateSandboxCommand("Clear Box", "clear-box", 200, null, null, 0, null));
        var sandboxId = createResult.Value.Id;

        var processUseCase = new ProcessSandboxRequestUseCase(_db, _dt);
        await processUseCase.ExecuteAsync("clear-box", "POST", "/api/v1/sandbox/receiver/clear-box", null, "{}", "{}", "application/json", 2, "127.0.0.1");
        await processUseCase.ExecuteAsync("clear-box", "POST", "/api/v1/sandbox/receiver/clear-box", null, "{}", "{}", "application/json", 2, "127.0.0.1");

        var clearUseCase = new ClearSandboxRequestsUseCase(_db, _tenantContext);

        // Act
        var clearResult = await clearUseCase.ExecuteAsync(sandboxId);

        // Assert
        clearResult.IsSuccess.Should().BeTrue();
        var remaining = await _db.SandboxRequests.Where(r => r.SandboxId == sandboxId).CountAsync();
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task DeleteSandbox_ShouldCascadeDeleteSandboxAndItsRequests()
    {
        // Arrange
        var createUseCase = new CreateSandboxUseCase(_db, _tenantContext, _dt);
        var createResult = await createUseCase.ExecuteAsync(new CreateSandboxCommand("Delete Box", "delete-box", 200, null, null, 0, null));
        var sandboxId = createResult.Value.Id;

        var processUseCase = new ProcessSandboxRequestUseCase(_db, _dt);
        await processUseCase.ExecuteAsync("delete-box", "POST", "/api/v1/sandbox/receiver/delete-box", null, "{}", "{}", "application/json", 2, "127.0.0.1");

        var deleteUseCase = new DeleteSandboxUseCase(_db, _tenantContext);

        // Act
        var deleteResult = await deleteUseCase.ExecuteAsync(sandboxId);

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();
        var sandboxInDb = await _db.WebhookSandboxes.FindAsync(sandboxId);
        sandboxInDb.Should().BeNull();

        var requestsInDb = await _db.SandboxRequests.Where(r => r.SandboxId == sandboxId).ToListAsync();
        requestsInDb.Should().BeEmpty();
    }
}
