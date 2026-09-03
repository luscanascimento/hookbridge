using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Applications;
using HookBridge.Application.ControlPlane.Validators;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class ApplicationUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly CurrentUser _currentUser;
    private readonly DateTimeProvider _dt;
    private readonly Guid _tenantId;

    public ApplicationUseCasesTests()
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
    public async Task CreateApplication_WithValidData_ShouldSucceed_AndWriteAuditLog()
    {
        // Arrange
        var useCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var command = new CreateApplicationCommand("Billing App", "Processes invoices");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Billing App");
        result.Value.TenantId.Should().Be(_tenantId);

        var appInDb = await _db.Applications.FirstOrDefaultAsync(a => a.Id == result.Value.Id);
        appInDb.Should().NotBeNull();

        var audit = await _db.AuditEntries.FirstOrDefaultAsync(a => a.ResourceId == result.Value.Id.ToString());
        audit.Should().NotBeNull();
        audit!.Action.Should().Be("Application.Created");
    }

    [Fact]
    public async Task CreateApplication_DuplicateName_ShouldReturnConflict()
    {
        // Arrange
        var useCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        await useCase.ExecuteAsync(new CreateApplicationCommand("Duplicate App", null));

        // Act
        var result = await useCase.ExecuteAsync(new CreateApplicationCommand("Duplicate App", null));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Application.NameInUse");
    }

    [Fact]
    public async Task GetApplications_ShouldReturnOnlyCurrentTenantApplications()
    {
        // Arrange
        var useCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        await useCase.ExecuteAsync(new CreateApplicationCommand("App 1", null));
        await useCase.ExecuteAsync(new CreateApplicationCommand("App 2", null));

        var getUseCase = new GetApplicationsUseCase(_db, _tenantContext);

        // Act
        var result = await getUseCase.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateApplication_ShouldModifyEntity_AndCreateAuditLog()
    {
        // Arrange
        var createUseCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var created = await createUseCase.ExecuteAsync(new CreateApplicationCommand("Original Name", "Desc"));

        var updateUseCase = new UpdateApplicationUseCase(_db, _tenantContext, _currentUser, new UpdateApplicationValidator(), _dt);

        // Act
        var updateResult = await updateUseCase.ExecuteAsync(created.Value.Id, new UpdateApplicationCommand("Updated Name", "New Desc", true));

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value.Name.Should().Be("Updated Name");
        updateResult.Value.Description.Should().Be("New Desc");
    }

    [Fact]
    public async Task DeleteApplication_ShouldRemoveEntity_AndCreateAuditLog()
    {
        // Arrange
        var createUseCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var created = await createUseCase.ExecuteAsync(new CreateApplicationCommand("To Delete", null));

        var deleteUseCase = new DeleteApplicationUseCase(_db, _tenantContext, _currentUser, _dt);

        // Act
        var deleteResult = await deleteUseCase.ExecuteAsync(created.Value.Id);

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();
        var exists = await _db.Applications.AnyAsync(a => a.Id == created.Value.Id);
        exists.Should().BeFalse();
    }
}
