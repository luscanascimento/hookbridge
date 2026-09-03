using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Applications;
using HookBridge.Application.ControlPlane.UseCases.Endpoints;
using HookBridge.Application.ControlPlane.UseCases.WebhookSecrets;
using HookBridge.Application.ControlPlane.Validators;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class WebhookSecretUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly CurrentUser _currentUser;
    private readonly DateTimeProvider _dt;
    private readonly KeyGenerator _keyGenerator;
    private readonly AesSecretEncryptor _secretEncryptor;
    private readonly SsrfGuard _ssrfGuard;
    private readonly Guid _tenantId;

    public WebhookSecretUseCasesTests()
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
        _secretEncryptor = new AesSecretEncryptor(Options.Create(new WebhookEncryptionOptions()));
        _ssrfGuard = new SsrfGuard(Options.Create(new SsrfOptions { ResolveDns = false }), NullLogger<SsrfGuard>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task RotateWebhookSecret_ShouldTransitionOldToRotating_AndCreateVersion2()
    {
        // Arrange
        var appUseCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var app = await appUseCase.ExecuteAsync(new CreateApplicationCommand("Test App", null));

        var endpointUseCase = new CreateEndpointUseCase(_db, _tenantContext, _currentUser, new CreateEndpointValidator(), _ssrfGuard, _keyGenerator, _secretEncryptor, _dt);
        var endpoint = await endpointUseCase.ExecuteAsync(new CreateEndpointCommand(app.Value.Id, "https://api.acme.com/webhooks"));

        var rotateUseCase = new RotateWebhookSecretUseCase(_db, _tenantContext, _currentUser, _keyGenerator, _secretEncryptor, _dt);

        // Act
        var rotateResult = await rotateUseCase.ExecuteAsync(endpoint.Value.Id);

        // Assert
        rotateResult.IsSuccess.Should().BeTrue();
        rotateResult.Value.Version.Should().Be(2);
        rotateResult.Value.Status.Should().Be(SecretStatus.Active);
        rotateResult.Value.NewSecret.Should().StartWith("whsec_");

        var secretsInDb = await _db.WebhookSecrets
            .Where(s => s.EndpointId == endpoint.Value.Id)
            .OrderBy(s => s.Version)
            .ToListAsync();

        secretsInDb.Should().HaveCount(2);
        secretsInDb[0].Version.Should().Be(1);
        secretsInDb[0].Status.Should().Be(SecretStatus.Rotating);
        secretsInDb[1].Version.Should().Be(2);
        secretsInDb[1].Status.Should().Be(SecretStatus.Active);
    }

    [Fact]
    public async Task RevokeWebhookSecret_ShouldSetStatusToRevoked()
    {
        // Arrange
        var appUseCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var app = await appUseCase.ExecuteAsync(new CreateApplicationCommand("Test App", null));

        var endpointUseCase = new CreateEndpointUseCase(_db, _tenantContext, _currentUser, new CreateEndpointValidator(), _ssrfGuard, _keyGenerator, _secretEncryptor, _dt);
        var endpoint = await endpointUseCase.ExecuteAsync(new CreateEndpointCommand(app.Value.Id, "https://api.acme.com/webhooks"));

        var getSecrets = new GetEndpointSecretsUseCase(_db, _tenantContext);
        var list = await getSecrets.ExecuteAsync(endpoint.Value.Id);
        var secretId = list.Value[0].Id;

        var revokeUseCase = new RevokeWebhookSecretUseCase(_db, _tenantContext, _currentUser, _dt);

        // Act
        var revokeResult = await revokeUseCase.ExecuteAsync(endpoint.Value.Id, secretId);

        // Assert
        revokeResult.IsSuccess.Should().BeTrue();
        var secretInDb = await _db.WebhookSecrets.FindAsync(secretId);
        secretInDb!.Status.Should().Be(SecretStatus.Revoked);
        secretInDb.RevokedAt.Should().NotBeNull();
    }
}
