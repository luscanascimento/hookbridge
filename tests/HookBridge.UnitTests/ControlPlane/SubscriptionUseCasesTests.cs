using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Applications;
using HookBridge.Application.ControlPlane.UseCases.Endpoints;
using HookBridge.Application.ControlPlane.UseCases.Subscriptions;
using HookBridge.Application.ControlPlane.Validators;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class SubscriptionUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly CurrentUser _currentUser;
    private readonly DateTimeProvider _dt;
    private readonly Guid _tenantId;

    public SubscriptionUseCasesTests()
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
    public async Task CreateSubscription_WithValidPattern_ShouldSucceed()
    {
        // Arrange
        var appUseCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var app = await appUseCase.ExecuteAsync(new CreateApplicationCommand("Test App", null));

        var keyGen = new KeyGenerator();
        var encryptor = new AesSecretEncryptor(Options.Create(new WebhookEncryptionOptions()));
        var ssrf = new SsrfGuard(Options.Create(new SsrfOptions { ResolveDns = false }), NullLogger<SsrfGuard>.Instance);

        var endpointUseCase = new CreateEndpointUseCase(_db, _tenantContext, _currentUser, new CreateEndpointValidator(), ssrf, keyGen, encryptor, _dt);
        var endpoint = await endpointUseCase.ExecuteAsync(new CreateEndpointCommand(app.Value.Id, "https://api.acme.com/webhooks"));

        var subUseCase = new CreateSubscriptionUseCase(_db, _tenantContext, _currentUser, new CreateSubscriptionValidator(), _dt);

        // Act
        var result = await subUseCase.ExecuteAsync(endpoint.Value.Id, new CreateSubscriptionCommand("invoice.paid"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EventTypePattern.Should().Be("invoice.paid");
        result.Value.EndpointId.Should().Be(endpoint.Value.Id);
    }

    [Fact]
    public async Task CreateSubscription_DuplicatePatternOnSameEndpoint_ShouldFailWithConflict()
    {
        // Arrange
        var appUseCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var app = await appUseCase.ExecuteAsync(new CreateApplicationCommand("Test App", null));

        var keyGen = new KeyGenerator();
        var encryptor = new AesSecretEncryptor(Options.Create(new WebhookEncryptionOptions()));
        var ssrf = new SsrfGuard(Options.Create(new SsrfOptions { ResolveDns = false }), NullLogger<SsrfGuard>.Instance);

        var endpointUseCase = new CreateEndpointUseCase(_db, _tenantContext, _currentUser, new CreateEndpointValidator(), ssrf, keyGen, encryptor, _dt);
        var endpoint = await endpointUseCase.ExecuteAsync(new CreateEndpointCommand(app.Value.Id, "https://api.acme.com/webhooks"));

        var subUseCase = new CreateSubscriptionUseCase(_db, _tenantContext, _currentUser, new CreateSubscriptionValidator(), _dt);
        await subUseCase.ExecuteAsync(endpoint.Value.Id, new CreateSubscriptionCommand("user.created"));

        // Act
        var result = await subUseCase.ExecuteAsync(endpoint.Value.Id, new CreateSubscriptionCommand("user.created"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Subscription.AlreadyExists");
    }
}
