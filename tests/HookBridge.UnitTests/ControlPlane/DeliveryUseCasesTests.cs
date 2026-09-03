using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Deliveries;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class DeliveryUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly DateTimeProvider _dt;
    private readonly Guid _tenantId;

    public DeliveryUseCasesTests()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "test-tenant");

        _db = new HookBridgeDbContext(options, _tenantContext);
        _dt = new DateTimeProvider();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GetDeliveries_WithFilters_ShouldReturnFilteredResults()
    {
        // Arrange
        var now = _dt.UtcNow;
        var epId1 = Guid.NewGuid();
        var epId2 = Guid.NewGuid();

        var del1 = Delivery.Create(_tenantId, Guid.NewGuid(), epId1, Guid.NewGuid(), "order.created", "corr1", null, now).Value;
        var del2 = Delivery.Create(_tenantId, Guid.NewGuid(), epId2, Guid.NewGuid(), "invoice.paid", "corr2", null, now).Value;
        var otherTenantDel = Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), epId1, Guid.NewGuid(), "order.created", "corr3", null, now).Value;

        _db.Deliveries.AddRange(del1, del2, otherTenantDel);
        await _db.SaveChangesAsync();

        var useCase = new GetDeliveriesUseCase(_db, _tenantContext);

        // Act - Filter by EventType
        var result = await useCase.ExecuteAsync(new GetDeliveriesQuery(EventType: "order.created"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle(d => d.Id == del1.Id);
    }

    [Fact]
    public async Task GetDeliveryById_ShouldReturnDetailsWithAttempts()
    {
        // Arrange
        var now = _dt.UtcNow;
        var del = Delivery.Create(_tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "payment.settled", "corr1", null, now).Value;
        _db.Deliveries.Add(del);
        await _db.SaveChangesAsync();

        var recordUseCase = new RecordDeliveryAttemptUseCase(_db, _tenantContext, _dt);
        await recordUseCase.ExecuteAsync(del.Id, new RecordDeliveryAttemptCommand(
            200, "{\"Content-Type\":\"application/json\"}", "{\"amount\":100}", "{\"Status\":\"OK\"}", "{\"received\":true}", 32, null, DeliveryStatus.Success));

        var getDetailUseCase = new GetDeliveryByIdUseCase(_db, _tenantContext);

        // Act
        var result = await getDetailUseCase.ExecuteAsync(del.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(DeliveryStatus.Success);
        result.Value.Attempts.Should().ContainSingle();
        result.Value.Attempts[0].HttpStatusCode.Should().Be(200);
        result.Value.Attempts[0].ElapsedMs.Should().Be(32);
    }

    [Fact]
    public async Task GetDeliveryStats_ShouldCalculateSuccessRateAndLatency()
    {
        // Arrange
        var now = _dt.UtcNow;
        var del1 = Delivery.Create(_tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "order.created", "corr1", null, now).Value;
        var del2 = Delivery.Create(_tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "order.created", "corr2", null, now).Value;
        _db.Deliveries.AddRange(del1, del2);
        await _db.SaveChangesAsync();

        var recordUseCase = new RecordDeliveryAttemptUseCase(_db, _tenantContext, _dt);
        // Attempt 1: Success (50ms)
        await recordUseCase.ExecuteAsync(del1.Id, new RecordDeliveryAttemptCommand(200, "{}", "{}", null, null, 50, null, DeliveryStatus.Success));
        // Attempt 2: Failed (150ms)
        await recordUseCase.ExecuteAsync(del2.Id, new RecordDeliveryAttemptCommand(500, "{}", "{}", null, null, 150, "Internal Server Error", DeliveryStatus.Failed));

        var statsUseCase = new GetDeliveryStatsUseCase(_db, _tenantContext);

        // Act
        var stats = await statsUseCase.ExecuteAsync();

        // Assert
        stats.IsSuccess.Should().BeTrue();
        stats.Value.TotalDeliveries.Should().Be(2);
        stats.Value.SuccessfulDeliveries.Should().Be(1);
        stats.Value.FailedDeliveries.Should().Be(1);
        stats.Value.SuccessRatePercentage.Should().Be(50.0);
        stats.Value.AverageLatencyMs.Should().Be(100.0); // (50 + 150) / 2
    }
}
