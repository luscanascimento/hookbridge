using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Applications;
using HookBridge.Application.ControlPlane.UseCases.Endpoints;
using HookBridge.Application.ControlPlane.UseCases.Publishing;
using HookBridge.Application.ControlPlane.Validators;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class PublishEventUseCaseTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly CurrentUser _currentUser;
    private readonly DateTimeProvider _dt;
    private readonly Guid _tenantId;

    private sealed class StubEventFlowClient : IEventFlowClient
    {
        public EventFlowIngestRequest? LastRequest { get; private set; }

        public Task<Result<EventFlowIngestResponse>> IngestEventAsync(EventFlowIngestRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result.Success(new EventFlowIngestResponse(
                request.EventId ?? Guid.NewGuid(),
                "Accepted",
                DateTimeOffset.UtcNow)));
        }

        public Task<Result<IReadOnlyList<DeadLetterMessageDto>>> PeekDlqAsync(int count = 10, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<DeadLetterMessageDto>>(Array.Empty<DeadLetterMessageDto>()));

        public Task<Result<int>> ReplayDlqAsync(int maxCount = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(0));

        public Task<Result<int>> PurgeDlqAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(0));
    }

    public PublishEventUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "test-tenant");

        _currentUser = new CurrentUser
        {
            UserId = Guid.NewGuid(),
            Email = "admin@test.com"
        };

        _db = new HookBridgeDbContext(options, _tenantContext);
        _dt = new DateTimeProvider();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task PublishEvent_WithMatchingEndpointSubscription_ShouldCreateDeliveryAndForwardToEventFlow()
    {
        // Arrange
        var appUseCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var app = await appUseCase.ExecuteAsync(new CreateApplicationCommand("Test App", null));

        var keyGen = new KeyGenerator();
        var encryptor = new AesSecretEncryptor(Options.Create(new WebhookEncryptionOptions()));
        var ssrf = new SsrfGuard(Options.Create(new SsrfOptions { ResolveDns = false }), NullLogger<SsrfGuard>.Instance);

        var endpointUseCase = new CreateEndpointUseCase(_db, _tenantContext, _currentUser, new CreateEndpointValidator(), ssrf, keyGen, encryptor, _dt);
        await endpointUseCase.ExecuteAsync(new CreateEndpointCommand(
            app.Value.Id,
            "https://api.acme.com/webhooks",
            "Order Webhooks",
            600,
            15,
            new List<string> { "order.*" }));

        var stubEventFlow = new StubEventFlowClient();
        var publishUseCase = new PublishEventUseCase(
            _db, _tenantContext, _currentUser, stubEventFlow, new PublishEventValidator(), _dt);

        var payload = JsonDocument.Parse("{\"orderId\":\"ord_123\",\"amount\":99.9}").RootElement;
        var command = new PublishEventCommand("order.created", payload, "idemp_test_1");

        // Act
        var result = await publishUseCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EventType.Should().Be("order.created");
        result.Value.DeliveriesScheduled.Should().Be(1);

        stubEventFlow.LastRequest.Should().NotBeNull();
        stubEventFlow.LastRequest!.EventType.Should().Be("order.created");
        stubEventFlow.LastRequest.TenantId.Should().Be(_tenantId.ToString());

        var deliveriesInDb = await _db.Deliveries.ToListAsync();
        deliveriesInDb.Should().ContainSingle();
        deliveriesInDb[0].EventType.Should().Be("order.created");
        deliveriesInDb[0].Status.Should().Be(DeliveryStatus.Pending);

        var auditInDb = await _db.AuditEntries.FirstOrDefaultAsync(a => a.Action == "Event.Published");
        auditInDb.Should().NotBeNull();
    }
}
