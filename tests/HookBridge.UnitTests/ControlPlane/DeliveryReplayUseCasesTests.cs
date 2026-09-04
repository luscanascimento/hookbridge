using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Deliveries;
using HookBridge.Application.ControlPlane.Validators;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class DeliveryReplayUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IEventFlowClient _eventFlowClient;
    private readonly DateTimeProvider _dt;
    private readonly Guid _tenantId;
    private readonly Guid _userId;

    public DeliveryReplayUseCasesTests()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        _userId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "test-tenant");

        _currentUser = Substitute.For<ICurrentUser>();
        _currentUser.UserId.Returns(_userId);

        _eventFlowClient = Substitute.For<IEventFlowClient>();
        _eventFlowClient.IngestEventAsync(Arg.Any<EventFlowIngestRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new EventFlowIngestResponse(Guid.NewGuid(), "Accepted", DateTimeOffset.UtcNow))));

        _db = new HookBridgeDbContext(options, _tenantContext);
        _dt = new DateTimeProvider();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task ReplayDelivery_Success_ShouldCreateLinkedDeliveryAndCallEventFlow()
    {
        // Arrange
        var now = _dt.UtcNow;
        var appId = Guid.NewGuid();
        var endpoint = Endpoint.Create(_tenantId, appId, "https://api.example.com/webhooks", "Primary", now, 600, 10).Value;
        var subscription = Subscription.Create(_tenantId, endpoint.Id, "order.*", now).Value;
        var originalDelivery = Delivery.Create(_tenantId, Guid.NewGuid(), endpoint.Id, subscription.Id, "order.placed", "corr_123", null, now).Value;
        originalDelivery.MarkFailed(now);

        var attempt = Attempt.Create(
            originalDelivery.Id,
            _tenantId,
            1,
            500,
            "{\"Content-Type\":\"application/json\"}",
            "{\"orderId\":\"ord_999\",\"amount\":199.90}",
            null,
            "Internal Server Error",
            120,
            "Target server responded 500",
            now).Value;

        _db.Endpoints.Add(endpoint);
        _db.Subscriptions.Add(subscription);
        _db.Deliveries.Add(originalDelivery);
        _db.Attempts.Add(attempt);
        await _db.SaveChangesAsync();

        var validator = new ReplayDeliveryValidator();
        var useCase = new ReplayDeliveryUseCase(_db, _tenantContext, _currentUser, _eventFlowClient, validator, _dt);

        // Act
        var result = await useCase.ExecuteAsync(originalDelivery.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OriginalDeliveryId.Should().Be(originalDelivery.Id);
        result.Value.DeliveryId.Should().NotBe(originalDelivery.Id);
        result.Value.EventType.Should().Be("order.placed");
        result.Value.Status.Should().Be(DeliveryStatus.Pending);

        // Verify EventFlow client called with original payload
        await _eventFlowClient.Received(1).IngestEventAsync(
            Arg.Is<EventFlowIngestRequest>(r =>
                r.EventType == "order.placed" &&
                r.TenantId == _tenantId.ToString() &&
                r.Payload.GetProperty("orderId").GetString() == "ord_999"),
            Arg.Any<CancellationToken>());

        // Verify Audit entry persisted
        var audit = await _db.AuditEntries.FirstOrDefaultAsync(a => a.Action == "Delivery.Replayed");
        audit.Should().NotBeNull();
        audit!.ResourceId.Should().Be(result.Value.DeliveryId.ToString());
    }

    [Fact]
    public async Task ReplayDelivery_WithDisabledEndpoint_ShouldFail()
    {
        // Arrange
        var now = _dt.UtcNow;
        var appId = Guid.NewGuid();
        var endpoint = Endpoint.Create(_tenantId, appId, "https://api.example.com/webhooks", "Primary", now, 600, 10).Value;
        endpoint.SetStatus(EndpointStatus.Disabled, "Repeated timeouts", now);

        var delivery = Delivery.Create(_tenantId, Guid.NewGuid(), endpoint.Id, Guid.NewGuid(), "order.placed", "corr_123", null, now).Value;

        _db.Endpoints.Add(endpoint);
        _db.Deliveries.Add(delivery);
        await _db.SaveChangesAsync();

        var validator = new ReplayDeliveryValidator();
        var useCase = new ReplayDeliveryUseCase(_db, _tenantContext, _currentUser, _eventFlowClient, validator, _dt);

        // Act
        var result = await useCase.ExecuteAsync(delivery.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Endpoint.Disabled");
    }

    [Fact]
    public async Task ReplayDelivery_WithOverrideEndpoint_ShouldReplayToNewEndpoint()
    {
        // Arrange
        var now = _dt.UtcNow;
        var appId = Guid.NewGuid();
        var ep1 = Endpoint.Create(_tenantId, appId, "https://api.example.com/endpoint1", "EP1", now, 600, 10).Value;
        var ep2 = Endpoint.Create(_tenantId, appId, "https://api.example.com/endpoint2", "EP2", now, 600, 10).Value;

        var delivery = Delivery.Create(_tenantId, Guid.NewGuid(), ep1.Id, Guid.NewGuid(), "order.placed", "corr_123", null, now).Value;

        _db.Endpoints.AddRange(ep1, ep2);
        _db.Deliveries.Add(delivery);
        await _db.SaveChangesAsync();

        var validator = new ReplayDeliveryValidator();
        var useCase = new ReplayDeliveryUseCase(_db, _tenantContext, _currentUser, _eventFlowClient, validator, _dt);

        // Act
        var result = await useCase.ExecuteAsync(delivery.Id, new ReplayDeliveryCommand(ep2.Id));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EndpointId.Should().Be(ep2.Id);
    }

    [Fact]
    public async Task ReplayDelivery_CrossTenant_ShouldReturnNotFound()
    {
        // Arrange
        var now = _dt.UtcNow;
        var otherTenantId = Guid.NewGuid();
        var foreignDelivery = Delivery.Create(otherTenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "user.created", "corr_xyz", null, now).Value;

        _db.Deliveries.Add(foreignDelivery);
        await _db.SaveChangesAsync();

        var validator = new ReplayDeliveryValidator();
        var useCase = new ReplayDeliveryUseCase(_db, _tenantContext, _currentUser, _eventFlowClient, validator, _dt);

        // Act
        var result = await useCase.ExecuteAsync(foreignDelivery.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Delivery.NotFound");
    }

    [Fact]
    public async Task BulkReplayDeliveries_ShouldReplayAllFailedAndDeadLettered()
    {
        // Arrange
        var now = _dt.UtcNow;
        var appId = Guid.NewGuid();
        var endpoint = Endpoint.Create(_tenantId, appId, "https://api.example.com/webhooks", "BulkEP", now, 600, 10).Value;

        var delSuccess = Delivery.Create(_tenantId, Guid.NewGuid(), endpoint.Id, Guid.NewGuid(), "order.placed", "corr_1", null, now).Value;
        delSuccess.MarkSuccess(now);

        var delFailed1 = Delivery.Create(_tenantId, Guid.NewGuid(), endpoint.Id, Guid.NewGuid(), "order.placed", "corr_2", null, now).Value;
        delFailed1.MarkFailed(now);

        var delFailed2 = Delivery.Create(_tenantId, Guid.NewGuid(), endpoint.Id, Guid.NewGuid(), "order.placed", "corr_3", null, now).Value;
        delFailed2.MarkDeadLettered(now);

        _db.Endpoints.Add(endpoint);
        _db.Deliveries.AddRange(delSuccess, delFailed1, delFailed2);
        await _db.SaveChangesAsync();

        var validator = new BulkReplayDeliveriesValidator();
        var useCase = new BulkReplayDeliveriesUseCase(_db, _tenantContext, _currentUser, _eventFlowClient, validator, _dt);

        // Act - Bulk Replay with default status filter (Failed & DeadLettered)
        var result = await useCase.ExecuteAsync(new BulkReplayDeliveriesCommand());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReplayedCount.Should().Be(2);
        result.Value.ReplayedDeliveries.Should().HaveCount(2);
        result.Value.ReplayedDeliveries.Select(d => d.OriginalDeliveryId).Should().Contain(new[] { delFailed1.Id, delFailed2.Id });

        var audit = await _db.AuditEntries.FirstOrDefaultAsync(a => a.Action == "Delivery.BulkReplayed");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task BulkReplayDeliveries_WithExplicitDeliveryIds_ShouldReplayOnlySpecified()
    {
        // Arrange
        var now = _dt.UtcNow;
        var appId = Guid.NewGuid();
        var endpoint = Endpoint.Create(_tenantId, appId, "https://api.example.com/webhooks", "BulkEP", now, 600, 10).Value;

        var del1 = Delivery.Create(_tenantId, Guid.NewGuid(), endpoint.Id, Guid.NewGuid(), "order.placed", "corr_1", null, now).Value;
        var del2 = Delivery.Create(_tenantId, Guid.NewGuid(), endpoint.Id, Guid.NewGuid(), "order.placed", "corr_2", null, now).Value;
        var del3 = Delivery.Create(_tenantId, Guid.NewGuid(), endpoint.Id, Guid.NewGuid(), "order.placed", "corr_3", null, now).Value;

        _db.Endpoints.Add(endpoint);
        _db.Deliveries.AddRange(del1, del2, del3);
        await _db.SaveChangesAsync();

        var validator = new BulkReplayDeliveriesValidator();
        var useCase = new BulkReplayDeliveriesUseCase(_db, _tenantContext, _currentUser, _eventFlowClient, validator, _dt);

        // Act
        var result = await useCase.ExecuteAsync(new BulkReplayDeliveriesCommand(DeliveryIds: new[] { del1.Id, del3.Id }));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReplayedCount.Should().Be(2);
        result.Value.ReplayedDeliveries.Select(d => d.OriginalDeliveryId).Should().BeEquivalentTo(new[] { del1.Id, del3.Id });
    }

    [Fact]
    public async Task GetDeliveryLineage_MultiGenerationChain_ShouldReturnOrderedLineage()
    {
        // Arrange
        var now = _dt.UtcNow;
        var eventId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var subId = Guid.NewGuid();

        // Gen 0 (Root)
        var rootDelivery = Delivery.Create(_tenantId, eventId, endpointId, subId, "payment.created", "corr_1", null, now.AddMinutes(-30)).Value;
        rootDelivery.MarkFailed(now.AddMinutes(-29));

        // Gen 1 (Replay of Root)
        var replay1 = Delivery.Create(_tenantId, eventId, endpointId, subId, "payment.created", "corr_1", null, now.AddMinutes(-20), rootDelivery.Id).Value;
        replay1.MarkFailed(now.AddMinutes(-19));

        // Gen 2 (Replay of Replay1)
        var replay2 = Delivery.Create(_tenantId, eventId, endpointId, subId, "payment.created", "corr_1", null, now.AddMinutes(-10), replay1.Id).Value;
        replay2.MarkSuccess(now.AddMinutes(-9));

        _db.Deliveries.AddRange(rootDelivery, replay1, replay2);
        await _db.SaveChangesAsync();

        var lineageUseCase = new GetDeliveryLineageUseCase(_db, _tenantContext);

        // Act - Query from Gen 2
        var result = await lineageUseCase.ExecuteAsync(replay2.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RootDeliveryId.Should().Be(rootDelivery.Id);
        result.Value.LineageChain.Should().HaveCount(3);
        result.Value.LineageChain[0].Id.Should().Be(rootDelivery.Id);
        result.Value.LineageChain[1].Id.Should().Be(replay1.Id);
        result.Value.LineageChain[2].Id.Should().Be(replay2.Id);
    }
}
