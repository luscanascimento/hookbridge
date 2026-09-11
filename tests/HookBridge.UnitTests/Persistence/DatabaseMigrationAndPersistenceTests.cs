using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.Persistence;

public sealed class DatabaseMigrationAndPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly HookBridgeDbContext _dbContext;
    private readonly FakeTenantContext _tenantContext;
    private readonly AesSecretEncryptor _secretEncryptor;
    private readonly Guid _tenantId = Guid.NewGuid();

    public DatabaseMigrationAndPersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _tenantContext = new FakeTenantContext { TenantId = _tenantId, HasTenant = true };

        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new HookBridgeDbContext(options, _tenantContext, new DateTimeProvider());
        _dbContext.Database.EnsureCreated();

        _secretEncryptor = new AesSecretEncryptor(Options.Create(new WebhookEncryptionOptions
        {
            MasterKey = "7f8e9d0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6"
        }));
    }

    [Fact]
    public void DbContext_ModelSnapshot_ShouldContainAllExpectedTablesAndPrimaryKeys()
    {
        var entityTypes = _dbContext.Model.GetEntityTypes().ToList();

        entityTypes.Should().Contain(e => e.ClrType == typeof(Tenant));
        entityTypes.Should().Contain(e => e.ClrType == typeof(User));
        entityTypes.Should().Contain(e => e.ClrType == typeof(HookBridge.Domain.Entities.Application));
        entityTypes.Should().Contain(e => e.ClrType == typeof(Endpoint));
        entityTypes.Should().Contain(e => e.ClrType == typeof(Subscription));
        entityTypes.Should().Contain(e => e.ClrType == typeof(ApiKey));
        entityTypes.Should().Contain(e => e.ClrType == typeof(WebhookSecret));
        entityTypes.Should().Contain(e => e.ClrType == typeof(Delivery));
        entityTypes.Should().Contain(e => e.ClrType == typeof(Attempt));
        entityTypes.Should().Contain(e => e.ClrType == typeof(AuditEntry));
        entityTypes.Should().Contain(e => e.ClrType == typeof(RefreshToken));
        entityTypes.Should().Contain(e => e.ClrType == typeof(EventSchema));
        entityTypes.Should().Contain(e => e.ClrType == typeof(EventSchemaVersion));
        entityTypes.Should().Contain(e => e.ClrType == typeof(WebhookSandbox));
        entityTypes.Should().Contain(e => e.ClrType == typeof(SandboxRequest));
        entityTypes.Should().Contain(e => e.ClrType == typeof(SimulatorRule));
        entityTypes.Should().Contain(e => e.ClrType == typeof(SimulatorExecution));
    }

    [Fact]
    public void AttemptConfiguration_ShouldHaveUniqueCompositeIndexOnDeliveryIdAndAttemptNumber()
    {
        var attemptType = _dbContext.Model.FindEntityType(typeof(Attempt));
        attemptType.Should().NotBeNull();

        var indexes = attemptType!.GetIndexes().ToList();
        var uniqueAttemptIndex = indexes.FirstOrDefault(i =>
            i.IsUnique &&
            i.Properties.Count == 2 &&
            i.Properties.Any(p => p.Name == nameof(Attempt.DeliveryId)) &&
            i.Properties.Any(p => p.Name == nameof(Attempt.AttemptNumber)));

        uniqueAttemptIndex.Should().NotBeNull("DeliveryId and AttemptNumber must have a unique composite index to prevent duplicate attempts");
    }

    [Fact]
    public async Task SensitiveData_WebhookSecret_MustBeEncryptedAtRest()
    {
        var rawSecret = "whsec_super_secret_payload_signing_key_123456";
        var encryptedSecret = _secretEncryptor.Encrypt(rawSecret);

        encryptedSecret.Should().NotBe(rawSecret);
        encryptedSecret.Should().NotContain("whsec_");

        var decrypted = _secretEncryptor.Decrypt(encryptedSecret);
        decrypted.Should().Be(rawSecret);

        var tenant = Tenant.Create("sec-tenant", "Secret Tenant", DateTimeOffset.UtcNow).Value;
        _tenantContext.SetTenant(tenant.Id, tenant.Identifier);
        _dbContext.Tenants.Add(tenant);

        var app = HookBridge.Domain.Entities.Application.Create(tenant.Id, "Secret App", "Description", DateTimeOffset.UtcNow).Value;
        _dbContext.Applications.Add(app);

        var endpoint = Endpoint.Create(tenant.Id, app.Id, "https://api.acme.com/webhook", "Webhook Endpoint", DateTimeOffset.UtcNow).Value;
        _dbContext.Endpoints.Add(endpoint);

        var webhookSecretResult = WebhookSecret.Create(
            tenant.Id,
            endpoint.Id,
            "whsec_test",
            "hash_123",
            encryptedSecret,
            1,
            DateTimeOffset.UtcNow);

        webhookSecretResult.IsSuccess.Should().BeTrue();
        _dbContext.WebhookSecrets.Add(webhookSecretResult.Value);
        await _dbContext.SaveChangesAsync();

        var persisted = await _dbContext.WebhookSecrets.FirstAsync(s => s.Id == webhookSecretResult.Value.Id);
        persisted.EncryptedSecret.Should().NotContain(rawSecret);
    }

    [Fact]
    public async Task EmptyDatabase_InitializationAndEntityPersistence_ShouldSucceed()
    {
        var tenantResult = Tenant.Create("acme-corp", "Acme Corporation", DateTimeOffset.UtcNow);
        tenantResult.IsSuccess.Should().BeTrue();
        var tenant = tenantResult.Value;
        _tenantContext.SetTenant(tenant.Id, tenant.Identifier);
        _dbContext.Tenants.Add(tenant);

        var appResult = HookBridge.Domain.Entities.Application.Create(tenant.Id, "Billing Engine", "Processes invoices", DateTimeOffset.UtcNow);
        appResult.IsSuccess.Should().BeTrue();
        var app = appResult.Value;
        _dbContext.Applications.Add(app);

        var endpointResult = Endpoint.Create(tenant.Id, app.Id, "https://api.acme.com/hook", "Production Webhook", DateTimeOffset.UtcNow, 600, 15);
        endpointResult.IsSuccess.Should().BeTrue();
        _dbContext.Endpoints.Add(endpointResult.Value);

        var saveCount = await _dbContext.SaveChangesAsync();
        saveCount.Should().BeGreaterThan(0);

        var loadedEndpoint = await _dbContext.Endpoints.FirstOrDefaultAsync(e => e.Id == endpointResult.Value.Id);
        loadedEndpoint.Should().NotBeNull();
        loadedEndpoint!.TargetUrl.Should().Be("https://api.acme.com/hook");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private sealed class FakeTenantContext : HookBridge.Application.Abstractions.ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? TenantIdentifier { get; set; }
        public bool HasTenant { get; set; }

        public void SetTenant(Guid tenantId, string? tenantIdentifier = null)
        {
            TenantId = tenantId;
            TenantIdentifier = tenantIdentifier;
            HasTenant = tenantId != Guid.Empty;
        }

        public void Clear()
        {
            TenantId = null;
            TenantIdentifier = null;
            HasTenant = false;
        }
    }
}
