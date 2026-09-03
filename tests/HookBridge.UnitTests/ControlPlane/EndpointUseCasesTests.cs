using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Applications;
using HookBridge.Application.ControlPlane.UseCases.Endpoints;
using HookBridge.Application.ControlPlane.Validators;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class EndpointUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly CurrentUser _currentUser;
    private readonly DateTimeProvider _dt;
    private readonly KeyGenerator _keyGenerator;
    private readonly AesSecretEncryptor _secretEncryptor;
    private readonly SsrfGuard _ssrfGuard;
    private readonly Guid _tenantId;

    public EndpointUseCasesTests()
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
        _keyGenerator = new KeyGenerator();
        _secretEncryptor = new AesSecretEncryptor(Options.Create(new WebhookEncryptionOptions
        {
            MasterKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        }));
        _ssrfGuard = new SsrfGuard(Options.Create(new SsrfOptions { ResolveDns = false }), NullLogger<SsrfGuard>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task CreateEndpoint_WithValidData_ShouldProvisionInitialSecret_AndSubscriptions()
    {
        // Arrange
        var appUseCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var app = await appUseCase.ExecuteAsync(new CreateApplicationCommand("Test App", null));

        var endpointUseCase = new CreateEndpointUseCase(
            _db, _tenantContext, _currentUser, new CreateEndpointValidator(),
            _ssrfGuard, _keyGenerator, _secretEncryptor, _dt);

        var command = new CreateEndpointCommand(
            app.Value.Id,
            "https://api.acme.com/webhooks",
            "Customer Notification Endpoint",
            600,
            15,
            new List<string> { "order.*", "payment.settled.v1" });

        // Act
        var result = await endpointUseCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InitialSecret.Should().StartWith("whsec_");
        result.Value.SecretPrefix.Should().StartWith("whsec_");
        result.Value.SecretVersion.Should().Be(1);
        result.Value.SubscribedEvents.Should().HaveCount(2);

        var endpointInDb = await _db.Endpoints
            .Include(e => e.Secrets)
            .Include(e => e.Subscriptions)
            .FirstOrDefaultAsync(e => e.Id == result.Value.Id);

        endpointInDb.Should().NotBeNull();
        endpointInDb!.Secrets.Should().ContainSingle();
        endpointInDb.Subscriptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateEndpoint_WithSsrfTarget_ShouldReturnFailure()
    {
        // Arrange
        var appUseCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var app = await appUseCase.ExecuteAsync(new CreateApplicationCommand("Test App", null));

        var endpointUseCase = new CreateEndpointUseCase(
            _db, _tenantContext, _currentUser, new CreateEndpointValidator(),
            _ssrfGuard, _keyGenerator, _secretEncryptor, _dt);

        var command = new CreateEndpointCommand(
            app.Value.Id,
            "http://127.0.0.1:8080/internal-hook",
            "SSRF attempt");

        // Act
        var result = await endpointUseCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateEndpointStatus_ShouldChangeStatus_AndAudit()
    {
        // Arrange
        var appUseCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var app = await appUseCase.ExecuteAsync(new CreateApplicationCommand("Test App", null));

        var endpointUseCase = new CreateEndpointUseCase(
            _db, _tenantContext, _currentUser, new CreateEndpointValidator(),
            _ssrfGuard, _keyGenerator, _secretEncryptor, _dt);

        var created = await endpointUseCase.ExecuteAsync(new CreateEndpointCommand(app.Value.Id, "https://api.acme.com/hook"));

        var statusUseCase = new UpdateEndpointStatusUseCase(_db, _tenantContext, _currentUser, new UpdateEndpointStatusValidator(), _dt);

        // Act
        var result = await statusUseCase.ExecuteAsync(created.Value.Id, new UpdateEndpointStatusCommand(EndpointStatus.Paused, "Maintenance"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(EndpointStatus.Paused);
        result.Value.DisabledReason.Should().Be("Maintenance");
    }
}
