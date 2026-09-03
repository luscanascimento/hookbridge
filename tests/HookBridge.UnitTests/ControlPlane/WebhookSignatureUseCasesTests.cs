using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Applications;
using HookBridge.Application.ControlPlane.UseCases.Endpoints;
using HookBridge.Application.ControlPlane.UseCases.WebhookSecrets;
using HookBridge.Application.ControlPlane.UseCases.WebhookSigning;
using HookBridge.Application.ControlPlane.Validators;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class WebhookSignatureUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly CurrentUser _currentUser;
    private readonly DateTimeProvider _dt;
    private readonly KeyGenerator _keyGenerator;
    private readonly AesSecretEncryptor _secretEncryptor;
    private readonly WebhookSigner _webhookSigner;
    private readonly SsrfGuard _ssrfGuard;
    private readonly Guid _tenantId;

    public WebhookSignatureUseCasesTests()
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
        _webhookSigner = new WebhookSigner();
        _ssrfGuard = new SsrfGuard(Options.Create(new SsrfOptions { ResolveDns = false }), NullLogger<SsrfGuard>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GenerateAndVerifySignature_ShouldSucceed()
    {
        // Arrange
        var appUseCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var app = await appUseCase.ExecuteAsync(new CreateApplicationCommand("Test App", null));

        var endpointUseCase = new CreateEndpointUseCase(_db, _tenantContext, _currentUser, new CreateEndpointValidator(), _ssrfGuard, _keyGenerator, _secretEncryptor, _dt);
        var endpoint = await endpointUseCase.ExecuteAsync(new CreateEndpointCommand(app.Value.Id, "https://api.acme.com/webhooks"));

        var genUseCase = new GenerateEndpointSignatureUseCase(_db, _tenantContext, _webhookSigner, _secretEncryptor, _dt);
        var verifyUseCase = new VerifyEndpointSignatureUseCase(_db, _tenantContext, _webhookSigner, _secretEncryptor, _dt);

        var payload = "{\"event\":\"order.completed\",\"total\":99.90}";

        // Act - Generate
        var genResult = await genUseCase.ExecuteAsync(endpoint.Value.Id, new GenerateSignatureCommand(payload));

        // Assert - Generate
        genResult.IsSuccess.Should().BeTrue();
        genResult.Value.SignatureHeader.Should().StartWith("t=");
        genResult.Value.SecretsCount.Should().Be(1);

        // Act - Verify
        var verifyResult = await verifyUseCase.ExecuteAsync(endpoint.Value.Id, new VerifySignatureCommand(payload, genResult.Value.SignatureHeader));

        // Assert - Verify
        verifyResult.IsSuccess.Should().BeTrue();
        verifyResult.Value.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateSignature_AfterSecretRotation_ShouldEmitDualSignatures()
    {
        // Arrange
        var appUseCase = new CreateApplicationUseCase(_db, _tenantContext, _currentUser, new CreateApplicationValidator(), _dt);
        var app = await appUseCase.ExecuteAsync(new CreateApplicationCommand("Test App", null));

        var endpointUseCase = new CreateEndpointUseCase(_db, _tenantContext, _currentUser, new CreateEndpointValidator(), _ssrfGuard, _keyGenerator, _secretEncryptor, _dt);
        var endpoint = await endpointUseCase.ExecuteAsync(new CreateEndpointCommand(app.Value.Id, "https://api.acme.com/webhooks"));

        // Rotate Secret
        var rotateUseCase = new RotateWebhookSecretUseCase(_db, _tenantContext, _currentUser, _keyGenerator, _secretEncryptor, _dt);
        await rotateUseCase.ExecuteAsync(endpoint.Value.Id);

        var genUseCase = new GenerateEndpointSignatureUseCase(_db, _tenantContext, _webhookSigner, _secretEncryptor, _dt);
        var verifyUseCase = new VerifyEndpointSignatureUseCase(_db, _tenantContext, _webhookSigner, _secretEncryptor, _dt);

        var payload = "{\"event\":\"user.registered\"}";

        // Act
        var genResult = await genUseCase.ExecuteAsync(endpoint.Value.Id, new GenerateSignatureCommand(payload));

        // Assert
        genResult.IsSuccess.Should().BeTrue();
        genResult.Value.SecretsCount.Should().Be(2); // Active + Rotating
        genResult.Value.Signatures.Should().HaveCount(2);

        var verifyResult = await verifyUseCase.ExecuteAsync(endpoint.Value.Id, new VerifySignatureCommand(payload, genResult.Value.SignatureHeader));
        verifyResult.IsSuccess.Should().BeTrue();
        verifyResult.Value.IsValid.Should().BeTrue();
    }
}
