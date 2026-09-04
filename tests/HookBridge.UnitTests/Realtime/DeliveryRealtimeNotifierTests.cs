using FluentAssertions;
using HookBridge.Api.Hubs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace HookBridge.UnitTests.Realtime;

public sealed class DeliveryRealtimeNotifierTests
{
    private readonly IHubContext<DeliveryHub, IDeliveryHubClient> _hubContext;
    private readonly IDeliveryHubClient _tenantGroupClient;
    private readonly IDeliveryHubClient _endpointGroupClient;
    private readonly DeliveryRealtimeNotifier _sut;
    private readonly Guid _tenantId;
    private readonly Guid _endpointId;

    public DeliveryRealtimeNotifierTests()
    {
        _hubContext = Substitute.For<IHubContext<DeliveryHub, IDeliveryHubClient>>();
        var hubClients = Substitute.For<IHubClients<IDeliveryHubClient>>();
        _tenantGroupClient = Substitute.For<IDeliveryHubClient>();
        _endpointGroupClient = Substitute.For<IDeliveryHubClient>();

        _tenantId = Guid.NewGuid();
        _endpointId = Guid.NewGuid();

        hubClients.Group(DeliveryHub.GetTenantGroup(_tenantId)).Returns(_tenantGroupClient);
        hubClients.Group(DeliveryHub.GetEndpointGroup(_tenantId, _endpointId)).Returns(_endpointGroupClient);
        _hubContext.Clients.Returns(hubClients);

        _sut = new DeliveryRealtimeNotifier(_hubContext, NullLogger<DeliveryRealtimeNotifier>.Instance);
    }

    [Fact]
    public async Task NotifyDeliveryDispatchedAsync_ShouldBroadcastToTenantAndEndpointGroups()
    {
        // Arrange
        var delivery = Delivery.Create(
            _tenantId,
            Guid.NewGuid(),
            _endpointId,
            Guid.NewGuid(),
            "order.created",
            "corr_1",
            "00-trace-01",
            DateTimeOffset.UtcNow).Value;

        // Act
        await _sut.NotifyDeliveryDispatchedAsync(delivery);

        // Assert
        await _tenantGroupClient.Received(1).ReceiveDeliveryEvent(Arg.Is<RealtimeDeliveryEvent>(e =>
            e.EventType == "DeliveryDispatched" &&
            e.DeliveryId == delivery.Id &&
            e.TenantId == _tenantId &&
            e.EndpointId == _endpointId &&
            e.EventName == "order.created" &&
            e.Status == DeliveryStatus.Pending));

        await _tenantGroupClient.Received(1).DeliveryDispatched(Arg.Is<RealtimeDeliveryEvent>(e =>
            e.DeliveryId == delivery.Id));

        await _endpointGroupClient.Received(1).ReceiveDeliveryEvent(Arg.Is<RealtimeDeliveryEvent>(e =>
            e.DeliveryId == delivery.Id));

        await _endpointGroupClient.Received(1).DeliveryDispatched(Arg.Is<RealtimeDeliveryEvent>(e =>
            e.DeliveryId == delivery.Id));
    }

    [Fact]
    public async Task NotifyDeliveryAttemptRecordedAsync_ShouldBroadcastAttemptDetails()
    {
        // Arrange
        var delivery = Delivery.Create(
            _tenantId,
            Guid.NewGuid(),
            _endpointId,
            Guid.NewGuid(),
            "payment.settled",
            "corr_2",
            null,
            DateTimeOffset.UtcNow).Value;

        var attempt = Attempt.Create(
            delivery.Id,
            _tenantId,
            1,
            200,
            "{\"Content-Type\":\"application/json\"}",
            "{\"amount\":150}",
            "{\"status\":\"ok\"}",
            "{\"success\":true}",
            45,
            null,
            DateTimeOffset.UtcNow).Value;

        delivery.MarkSuccess(DateTimeOffset.UtcNow);

        // Act
        await _sut.NotifyDeliveryAttemptRecordedAsync(delivery, attempt);

        // Assert
        await _tenantGroupClient.Received(1).ReceiveDeliveryEvent(Arg.Is<RealtimeDeliveryEvent>(e =>
            e.EventType == "DeliveryAttemptRecorded" &&
            e.DeliveryId == delivery.Id &&
            e.Attempt != null &&
            e.Attempt.HttpStatusCode == 200 &&
            e.Attempt.ElapsedMs == 45));

        await _tenantGroupClient.Received(1).DeliveryAttemptRecorded(Arg.Is<RealtimeDeliveryEvent>(e =>
            e.DeliveryId == delivery.Id &&
            e.Attempt != null &&
            e.Attempt.AttemptNumber == 1));

        await _endpointGroupClient.Received(1).ReceiveDeliveryEvent(Arg.Is<RealtimeDeliveryEvent>(e =>
            e.DeliveryId == delivery.Id));
    }

    [Fact]
    public async Task NotifyDeliveryReplayedAsync_ShouldBroadcastReplayEventWithOriginalDeliveryId()
    {
        // Arrange
        var origId = Guid.NewGuid();
        var replayed = Delivery.Create(
            _tenantId,
            Guid.NewGuid(),
            _endpointId,
            Guid.NewGuid(),
            "user.registered",
            "corr_3",
            null,
            DateTimeOffset.UtcNow,
            origId).Value;

        // Act
        await _sut.NotifyDeliveryReplayedAsync(replayed, origId);

        // Assert
        await _tenantGroupClient.Received(1).ReceiveDeliveryEvent(Arg.Is<RealtimeDeliveryEvent>(e =>
            e.EventType == "DeliveryReplayed" &&
            e.DeliveryId == replayed.Id &&
            e.OriginalDeliveryId == origId));

        await _tenantGroupClient.Received(1).DeliveryReplayed(Arg.Is<RealtimeDeliveryEvent>(e =>
            e.DeliveryId == replayed.Id &&
            e.OriginalDeliveryId == origId));
    }

    [Fact]
    public async Task NotifyBulkDeliveriesReplayedAsync_ShouldBroadcastBatchAndIndividualEvents()
    {
        // Arrange
        var d1 = Delivery.Create(_tenantId, Guid.NewGuid(), _endpointId, Guid.NewGuid(), "e1", "c1", null, DateTimeOffset.UtcNow).Value;
        var d2 = Delivery.Create(_tenantId, Guid.NewGuid(), _endpointId, Guid.NewGuid(), "e2", "c2", null, DateTimeOffset.UtcNow).Value;
        var list = new List<Delivery> { d1, d2 };

        // Act
        await _sut.NotifyBulkDeliveriesReplayedAsync(_tenantId, list);

        // Assert
        await _tenantGroupClient.Received(1).BulkDeliveriesReplayed(Arg.Is<IReadOnlyList<RealtimeDeliveryEvent>>(l => l.Count == 2));
        await _tenantGroupClient.Received(2).ReceiveDeliveryEvent(Arg.Any<RealtimeDeliveryEvent>());
    }
}
