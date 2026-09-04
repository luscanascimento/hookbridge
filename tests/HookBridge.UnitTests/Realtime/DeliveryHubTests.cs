using System.Security.Claims;
using FluentAssertions;
using HookBridge.Api.Hubs;
using HookBridge.Domain.Entities;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using AppEntity = HookBridge.Domain.Entities.Application;

namespace HookBridge.UnitTests.Realtime;

public sealed class DeliveryHubTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly HubCallerContext _hubContext;
    private readonly IGroupManager _groupManager;
    private readonly DeliveryHub _hub;
    private readonly Guid _tenantId;
    private readonly Dictionary<object, object?> _items;

    public DeliveryHubTests()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "test-tenant");

        _db = new HookBridgeDbContext(options, _tenantContext);

        _hubContext = Substitute.For<HubCallerContext>();
        _groupManager = Substitute.For<IGroupManager>();
        _items = new Dictionary<object, object?>();

        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("tenant_id", _tenantId.ToString()),
            new(ClaimTypes.Role, "Developer")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _hubContext.User.Returns(principal);
        _hubContext.ConnectionId.Returns("conn-123");
        _hubContext.Items.Returns(_items);

        _hub = new DeliveryHub(_db, NullLogger<DeliveryHub>.Instance)
        {
            Context = _hubContext,
            Groups = _groupManager
        };
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task OnConnectedAsync_WithValidTenantClaim_ShouldJoinTenantGroup()
    {
        // Act
        await _hub.OnConnectedAsync();

        // Assert
        await _groupManager.Received(1).AddToGroupAsync("conn-123", DeliveryHub.GetTenantGroup(_tenantId));
        _items.Should().ContainKey(DeliveryHub.TenantIdItemKey);
        _items[DeliveryHub.TenantIdItemKey].Should().Be(_tenantId);
    }

    [Fact]
    public async Task OnConnectedAsync_WithoutTenantClaim_ShouldAbortConnection()
    {
        // Arrange
        var anonymousContext = Substitute.For<HubCallerContext>();
        anonymousContext.User.Returns(new ClaimsPrincipal());
        anonymousContext.ConnectionId.Returns("conn-anon");
        anonymousContext.Items.Returns(new Dictionary<object, object?>());

        var anonymousHub = new DeliveryHub(_db, NullLogger<DeliveryHub>.Instance)
        {
            Context = anonymousContext,
            Groups = _groupManager
        };

        // Act
        await anonymousHub.OnConnectedAsync();

        // Assert
        anonymousContext.Received(1).Abort();
        await _groupManager.DidNotReceive().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SubscribeToEndpoint_WithValidTenantEndpoint_ShouldAddToEndpointGroup()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var app = AppEntity.Create(_tenantId, "Test App", null, now).Value;
        var ep = Endpoint.Create(_tenantId, app.Id, "https://api.acme.com/webhook", "Orders", now, 600, 15).Value;
        _db.Applications.Add(app);
        _db.Endpoints.Add(ep);
        await _db.SaveChangesAsync();

        // Act
        var result = await _hub.SubscribeToEndpoint(ep.Id);

        // Assert
        result.Should().BeTrue();
        await _groupManager.Received(1).AddToGroupAsync("conn-123", DeliveryHub.GetEndpointGroup(_tenantId, ep.Id));
    }

    [Fact]
    public async Task SubscribeToEndpoint_WithForeignTenantEndpoint_ShouldRejectSubscription()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var otherTenantId = Guid.NewGuid();
        var app = AppEntity.Create(otherTenantId, "Foreign App", null, now).Value;
        var ep = Endpoint.Create(otherTenantId, app.Id, "https://foreign.com/webhook", "Foreign", now, 600, 15).Value;
        _db.Applications.Add(app);
        _db.Endpoints.Add(ep);
        await _db.SaveChangesAsync();

        // Act
        var result = await _hub.SubscribeToEndpoint(ep.Id);

        // Assert
        result.Should().BeFalse();
        await _groupManager.DidNotReceive().AddToGroupAsync("conn-123", DeliveryHub.GetEndpointGroup(_tenantId, ep.Id));
    }

    [Fact]
    public async Task UnsubscribeFromEndpoint_ShouldRemoveFromEndpointGroup()
    {
        // Arrange
        var endpointId = Guid.NewGuid();

        // Act
        var result = await _hub.UnsubscribeFromEndpoint(endpointId);

        // Assert
        result.Should().BeTrue();
        await _groupManager.Received(1).RemoveFromGroupAsync("conn-123", DeliveryHub.GetEndpointGroup(_tenantId, endpointId));
    }

    [Fact]
    public async Task SubscribeToApplication_WithValidTenantApp_ShouldAddToAppGroup()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var app = AppEntity.Create(_tenantId, "Billing App", null, now).Value;
        _db.Applications.Add(app);
        await _db.SaveChangesAsync();

        // Act
        var result = await _hub.SubscribeToApplication(app.Id);

        // Assert
        result.Should().BeTrue();
        await _groupManager.Received(1).AddToGroupAsync("conn-123", DeliveryHub.GetAppGroup(_tenantId, app.Id));
    }

    [Fact]
    public async Task SubscribeToApplication_WithForeignTenantApp_ShouldReject()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var otherTenantId = Guid.NewGuid();
        var app = AppEntity.Create(otherTenantId, "Foreign App", null, now).Value;
        _db.Applications.Add(app);
        await _db.SaveChangesAsync();

        // Act
        var result = await _hub.SubscribeToApplication(app.Id);

        // Assert
        result.Should().BeFalse();
        await _groupManager.DidNotReceive().AddToGroupAsync("conn-123", DeliveryHub.GetAppGroup(_tenantId, app.Id));
    }
}
