using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.UseCases.DeadLetter;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class DeadLetterUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly CurrentUser _currentUser;
    private readonly DateTimeProvider _dt;
    private readonly Guid _tenantId;

    private sealed class StubDlqEventFlowClient : IEventFlowClient
    {
        public Task<Result<EventFlowIngestResponse>> IngestEventAsync(EventFlowIngestRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new EventFlowIngestResponse(Guid.NewGuid(), "Accepted", DateTimeOffset.UtcNow)));

        public Task<Result<IReadOnlyList<DeadLetterMessageDto>>> PeekDlqAsync(int count = 10, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DeadLetterMessageDto> list = new List<DeadLetterMessageDto>
            {
                new("dlq_1", "eventflow.events", "payment.settled", "eventflow.events.dlq", "tenant-1", "payment.settled", "{}", null, null, "Failed", 3, DateTimeOffset.UtcNow)
            };
            return Task.FromResult(Result.Success(list));
        }

        public Task<Result<int>> ReplayDlqAsync(int maxCount = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(5));

        public Task<Result<int>> PurgeDlqAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(10));
    }

    public DeadLetterUseCasesTests()
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
    public async Task PeekDeadLetters_ShouldReturnMessages()
    {
        // Arrange
        var stub = new StubDlqEventFlowClient();
        var useCase = new PeekDeadLettersUseCase(stub, _tenantContext);

        // Act
        var result = await useCase.ExecuteAsync(10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(1);
    }

    [Fact]
    public async Task ReplayDeadLetters_ShouldAuditAndReturnCount()
    {
        // Arrange
        var stub = new StubDlqEventFlowClient();
        var useCase = new ReplayDeadLettersUseCase(_db, stub, _tenantContext, _currentUser, _dt);

        // Act
        var result = await useCase.ExecuteAsync(50);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReplayedCount.Should().Be(5);

        var audit = await _db.AuditEntries.FirstOrDefaultAsync(a => a.Action == "DLQ.Replayed");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task PurgeDeadLetters_ShouldAuditAndReturnCount()
    {
        // Arrange
        var stub = new StubDlqEventFlowClient();
        var useCase = new PurgeDeadLettersUseCase(_db, stub, _tenantContext, _currentUser, _dt);

        // Act
        var result = await useCase.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PurgedCount.Should().Be(10);

        var audit = await _db.AuditEntries.FirstOrDefaultAsync(a => a.Action == "DLQ.Purged");
        audit.Should().NotBeNull();
    }
}
