using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Deliveries;
using HookBridge.Application.ControlPlane.UseCases.Endpoints;
using HookBridge.Application.ControlPlane.UseCases.Publishing;
using HookBridge.Application.ControlPlane.Validators;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Integration;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ApplicationEntity = HookBridge.Domain.Entities.Application;

namespace HookBridge.UnitTests.Chaos;

public sealed class BrokerAndDbOutageChaosTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly DateTimeProvider _dateTimeProvider;
    private readonly Guid _tenantId;
    private readonly Guid _userId;

    public BrokerAndDbOutageChaosTests()
    {
        var dbOptions = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        _userId = Guid.NewGuid();

        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "chaos-tenant");

        _currentUser = Substitute.For<ICurrentUser>();
        _currentUser.UserId.Returns(_userId);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.Role.Returns(UserRole.TenantAdmin);

        _dateTimeProvider = new DateTimeProvider();

        _db = new HookBridgeDbContext(dbOptions, _tenantContext);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class FaultyHttpMessageHandler : HttpMessageHandler
    {
        public Exception? ExceptionToThrow { get; set; }
        public HttpResponseMessage? ResponseToReturn { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResponseToReturn ?? new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }

    private sealed class StubEventFlowClient : IEventFlowClient
    {
        public Result<EventFlowIngestResponse> IngestResult { get; set; } = Result.Success(new EventFlowIngestResponse(Guid.NewGuid(), "Accepted", DateTimeOffset.UtcNow));

        public Task<Result<EventFlowIngestResponse>> IngestEventAsync(EventFlowIngestRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(IngestResult);

        public Task<Result<IReadOnlyList<DeadLetterMessageDto>>> PeekDlqAsync(int count = 10, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure<IReadOnlyList<DeadLetterMessageDto>>(DomainError.Failure("EventFlow.ConnectionError", "Broker down")));

        public Task<Result<int>> ReplayDlqAsync(int maxCount = 50, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure<int>(DomainError.Failure("EventFlow.ConnectionError", "Broker down")));

        public Task<Result<int>> PurgeDlqAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure<int>(DomainError.Failure("EventFlow.ConnectionError", "Broker down")));
    }

    [Fact]
    public async Task EventFlowClient_SocketException_ReturnsConnectionError()
    {
        // Arrange
        var handler = new FaultyHttpMessageHandler
        {
            ExceptionToThrow = new HttpRequestException("Connection refused", new SocketException((int)SocketError.ConnectionRefused))
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://eventflow-broker:5000") };
        var options = Options.Create(new EventFlowOptions { ApiKey = "test_key", TimeoutSeconds = 5 });
        var client = new EventFlowClient(httpClient, options, NullLogger<EventFlowClient>.Instance);

        var payload = JsonDocument.Parse("{}").RootElement;
        var request = new EventFlowIngestRequest(
            Guid.NewGuid(), "order.created", 1, "test-source", DateTimeOffset.UtcNow,
            "corr_1", "trace_1", "tenant_1", "idemp_1", payload, null);

        // Act
        var result = await client.IngestEventAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EventFlow.ConnectionError");
        result.Error.Message.Should().Contain("Could not reach EventFlow Data Plane");
    }

    [Fact]
    public async Task EventFlowClient_Timeout_ReturnsConnectionError()
    {
        // Arrange
        var handler = new FaultyHttpMessageHandler
        {
            ExceptionToThrow = new TaskCanceledException("The request was canceled due to configured HttpClient.Timeout of 5 seconds.")
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://eventflow-broker:5000") };
        var options = Options.Create(new EventFlowOptions { ApiKey = "test_key", TimeoutSeconds = 5 });
        var client = new EventFlowClient(httpClient, options, NullLogger<EventFlowClient>.Instance);

        var payload = JsonDocument.Parse("{}").RootElement;
        var request = new EventFlowIngestRequest(
            Guid.NewGuid(), "order.created", 1, "test-source", DateTimeOffset.UtcNow,
            "corr_1", "trace_1", "tenant_1", "idemp_1", payload, null);

        // Act
        var result = await client.IngestEventAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EventFlow.ConnectionError");
    }

    [Fact]
    public async Task EventFlowClient_DlqOperations_OnOutage_ReturnFailureResults()
    {
        // Arrange
        var handler = new FaultyHttpMessageHandler
        {
            ExceptionToThrow = new HttpRequestException("Gateway Timeout", null, HttpStatusCode.GatewayTimeout)
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://eventflow-broker:5000") };
        var options = Options.Create(new EventFlowOptions { ApiKey = "test_key", TimeoutSeconds = 5 });
        var client = new EventFlowClient(httpClient, options, NullLogger<EventFlowClient>.Instance);

        // Act & Assert
        var peekRes = await client.PeekDlqAsync(10);
        peekRes.IsFailure.Should().BeTrue();
        peekRes.Error.Code.Should().Be("EventFlow.ConnectionError");

        var replayRes = await client.ReplayDlqAsync(50);
        replayRes.IsFailure.Should().BeTrue();
        replayRes.Error.Code.Should().BeOneOf("EventFlow.ConnectionError", "EventFlow.CircuitBroken");

        var purgeRes = await client.PurgeDlqAsync();
        purgeRes.IsFailure.Should().BeTrue();
        purgeRes.Error.Code.Should().BeOneOf("EventFlow.ConnectionError", "EventFlow.CircuitBroken");
    }

    [Fact]
    public async Task PublishEventUseCase_WhenEventFlowBrokerDown_ReturnsFailureResultGracefully()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        var app = ApplicationEntity.Create(_tenantId, "Store App", "Store", now).Value;
        _db.Applications.Add(app);

        var ep = Endpoint.Create(_tenantId, app.Id, "https://api.example.com/wh", "Webhook", now).Value;
        _db.Endpoints.Add(ep);

        var sub = Subscription.Create(_tenantId, ep.Id, "order.*", now).Value;
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();

        var faultyEventFlowClient = new StubEventFlowClient
        {
            IngestResult = Result.Failure<EventFlowIngestResponse>(DomainError.Failure("EventFlow.ConnectionError", "Broker unreachable"))
        };

        var useCase = new PublishEventUseCase(
            _db,
            _tenantContext,
            _currentUser,
            faultyEventFlowClient,
            new PublishEventValidator(),
            _dateTimeProvider);

        var payload = JsonDocument.Parse("{\"orderId\":\"ord_1\"}").RootElement;
        var command = new PublishEventCommand("order.created", payload, "idemp_1", 1, "corr_1");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EventFlow.ConnectionError");
    }

    [Fact]
    public async Task ReplayDeliveryUseCase_WhenBrokerDown_ReturnsFailureResult()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        var app = ApplicationEntity.Create(_tenantId, "App", "App", now).Value;
        _db.Applications.Add(app);

        var ep = Endpoint.Create(_tenantId, app.Id, "https://api.example.com/replay", "Replay EP", now).Value;
        _db.Endpoints.Add(ep);

        var sub = Subscription.Create(_tenantId, ep.Id, "payment.*", now).Value;
        _db.Subscriptions.Add(sub);

        var delivery = Delivery.Create(_tenantId, Guid.NewGuid(), ep.Id, sub.Id, "payment.success", "corr_rep", "trace_rep", now).Value;
        delivery.MarkFailed(now);
        _db.Deliveries.Add(delivery);

        var attempt = Attempt.Create(delivery.Id, _tenantId, 1, 500, "{}", "{}", "{}", "{}", 100, "Internal Error", now).Value;
        _db.Attempts.Add(attempt);
        await _db.SaveChangesAsync();

        var faultyEventFlowClient = new StubEventFlowClient
        {
            IngestResult = Result.Failure<EventFlowIngestResponse>(DomainError.Failure("EventFlow.ConnectionError", "Broker unavailable for replay"))
        };

        var useCase = new ReplayDeliveryUseCase(
            _db,
            _tenantContext,
            _currentUser,
            faultyEventFlowClient,
            new ReplayDeliveryValidator(),
            _dateTimeProvider);

        // Act
        var result = await useCase.ExecuteAsync(delivery.Id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EventFlow.ConnectionError");
    }

    [Fact]
    public async Task BulkReplayDeliveriesUseCase_WhenBrokerDown_ReturnsZeroReplayedGracefully()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        var app = ApplicationEntity.Create(_tenantId, "Bulk App", "Bulk App", now).Value;
        _db.Applications.Add(app);

        var ep = Endpoint.Create(_tenantId, app.Id, "https://api.example.com/bulk", "Bulk EP", now).Value;
        _db.Endpoints.Add(ep);

        var sub = Subscription.Create(_tenantId, ep.Id, "billing.*", now).Value;
        _db.Subscriptions.Add(sub);

        var delivery = Delivery.Create(_tenantId, Guid.NewGuid(), ep.Id, sub.Id, "billing.failed", "corr_bulk", "trace_bulk", now).Value;
        delivery.MarkFailed(now);
        _db.Deliveries.Add(delivery);

        var attempt = Attempt.Create(delivery.Id, _tenantId, 1, 503, "{}", "{}", "{}", "{}", 100, "Unavailable", now).Value;
        _db.Attempts.Add(attempt);
        await _db.SaveChangesAsync();

        var faultyEventFlowClient = new StubEventFlowClient
        {
            IngestResult = Result.Failure<EventFlowIngestResponse>(DomainError.Failure("EventFlow.ConnectionError", "Broker unavailable"))
        };

        var useCase = new BulkReplayDeliveriesUseCase(
            _db,
            _tenantContext,
            _currentUser,
            faultyEventFlowClient,
            new BulkReplayDeliveriesValidator(),
            _dateTimeProvider);

        var cmd = new BulkReplayDeliveriesCommand(
            DeliveryIds: new List<Guid> { delivery.Id }
        );

        // Act
        var result = await useCase.ExecuteAsync(cmd);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReplayedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetEndpointHealthMetricsUseCase_HandlesExtremeFailureOutages()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        var app = ApplicationEntity.Create(_tenantId, "Health App", "Health App", now).Value;
        _db.Applications.Add(app);

        var ep = Endpoint.Create(_tenantId, app.Id, "https://api.example.com/health-ep", "Health EP", now).Value;
        _db.Endpoints.Add(ep);

        var sub = Subscription.Create(_tenantId, ep.Id, "order.*", now).Value;
        _db.Subscriptions.Add(sub);

        // Inject 10 failed deliveries with high latency
        for (int i = 0; i < 10; i++)
        {
            var deliv = Delivery.Create(_tenantId, Guid.NewGuid(), ep.Id, sub.Id, "order.created", $"corr_{i}", $"trace_{i}", now.AddMinutes(-i * 5)).Value;
            deliv.MarkFailed(now.AddMinutes(-i * 5));
            _db.Deliveries.Add(deliv);

            var att = Attempt.Create(deliv.Id, _tenantId, 1, 500, "{}", "{}", "{}", "{}", 5000, "Server Error", now.AddMinutes(-i * 5)).Value;
            _db.Attempts.Add(att);
        }
        await _db.SaveChangesAsync();

        var useCase = new GetEndpointHealthMetricsUseCase(_db, _tenantContext, _dateTimeProvider);

        // Act
        var result = await useCase.ExecuteAsync(ep.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var health = result.Value;
        health.ConsecutiveFailures.Should().Be(10);
        health.CircuitState.Should().Be("Open");
        health.HealthScorePercent.Should().BeLessThan(30);
        health.Latencies.P99Ms.Should().BeGreaterThan(4000);
        health.Incidents.Should().NotBeEmpty();
    }
}
