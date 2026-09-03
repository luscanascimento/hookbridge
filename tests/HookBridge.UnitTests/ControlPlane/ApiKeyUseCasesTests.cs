using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.ApiKeys;
using HookBridge.Application.ControlPlane.Validators;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.UnitTests.ControlPlane;

public sealed class ApiKeyUseCasesTests : IDisposable
{
    private readonly HookBridgeDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly CurrentUser _currentUser;
    private readonly DateTimeProvider _dt;
    private readonly KeyGenerator _keyGenerator;
    private readonly Guid _tenantId;

    public ApiKeyUseCasesTests()
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
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task CreateApiKey_ShouldReturnPlaintextKey_AndStoreHash()
    {
        // Arrange
        var useCase = new CreateApiKeyUseCase(_db, _tenantContext, _currentUser, new CreateApiKeyValidator(), _keyGenerator, _dt);
        var command = new CreateApiKeyCommand("CI Ingest Key", ApiKeyScope.EventsIngest, "live");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Key.Should().StartWith("hb_live_");
        result.Value.KeyPrefix.Should().Be(result.Value.Key[..16]);

        var keyInDb = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == result.Value.Id);
        keyInDb.Should().NotBeNull();
        keyInDb!.KeyHash.Should().NotBeNullOrWhiteSpace();
        keyInDb.KeyHash.Should().NotBe(result.Value.Key); // Must NOT store plaintext
        keyInDb.KeyHash.Should().Be(_keyGenerator.ComputeHash(result.Value.Key));
    }

    [Fact]
    public async Task RevokeApiKey_ShouldSetRevokedAt_AndDeactivate()
    {
        // Arrange
        var createUseCase = new CreateApiKeyUseCase(_db, _tenantContext, _currentUser, new CreateApiKeyValidator(), _keyGenerator, _dt);
        var created = await createUseCase.ExecuteAsync(new CreateApiKeyCommand("Temp Key", ApiKeyScope.DeliveriesRead, "live"));

        var revokeUseCase = new RevokeApiKeyUseCase(_db, _tenantContext, _currentUser, _dt);

        // Act
        var result = await revokeUseCase.ExecuteAsync(created.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var keyInDb = await _db.ApiKeys.FindAsync(created.Value.Id);
        keyInDb!.RevokedAt.Should().NotBeNull();
        keyInDb.IsActive.Should().BeFalse();
    }
}
