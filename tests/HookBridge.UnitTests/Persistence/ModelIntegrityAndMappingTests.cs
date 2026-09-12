using FluentAssertions;
using HookBridge.Application.Common;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.UnitTests.Persistence;

public sealed class ModelIntegrityAndMappingTests : IDisposable
{
    private readonly HookBridgeDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly DateTimeProvider _dateTimeProvider;
    private readonly Guid _tenantId;

    public ModelIntegrityAndMappingTests()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "model-corp");
        _dateTimeProvider = new DateTimeProvider();

        _dbContext = new HookBridgeDbContext(options, _tenantContext, _dateTimeProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void All_TenantScoped_Entities_Must_Have_NonNullable_TenantId_And_QueryFilter()
    {
        // Arrange
        var entityTypes = _dbContext.Model.GetEntityTypes()
            .Where(e => typeof(ITenantScoped).IsAssignableFrom(e.ClrType))
            .ToList();

        entityTypes.Should().NotBeEmpty();

        // Assert
        foreach (var entityType in entityTypes)
        {
            var tenantIdProp = entityType.FindProperty("TenantId");
            tenantIdProp.Should().NotBeNull($"Entity {entityType.ClrType.Name} implements ITenantScoped but has no TenantId property.");
            tenantIdProp!.ClrType.Should().Be<Guid>();
            tenantIdProp.IsNullable.Should().BeFalse($"Entity {entityType.ClrType.Name} has a nullable TenantId, violating strict multi-tenancy.");

            var queryFilter = entityType.GetQueryFilter();
            queryFilter.Should().NotBeNull($"Entity {entityType.ClrType.Name} implements ITenantScoped but lacks a multi-tenant query filter.");
        }
    }

    [Fact]
    public void All_Entities_Must_Have_Primary_Key()
    {
        // Arrange
        var entityTypes = _dbContext.Model.GetEntityTypes().ToList();

        // Assert
        foreach (var entityType in entityTypes)
        {
            var pk = entityType.FindPrimaryKey();
            pk.Should().NotBeNull($"Entity {entityType.ClrType.Name} is missing a Primary Key configuration.");
            pk!.Properties.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void Business_Key_Unique_Constraints_Must_Be_Explicitly_Configured()
    {
        // 1. Tenant: Identifier must be unique
        var tenantType = _dbContext.Model.FindEntityType(typeof(Tenant));
        tenantType.Should().NotBeNull();
        tenantType!.GetIndexes()
            .Any(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(Tenant.Identifier)))
            .Should().BeTrue("Tenant.Identifier must have a unique index.");

        // 2. User: TenantId + Email must be unique
        var userType = _dbContext.Model.FindEntityType(typeof(User));
        userType.Should().NotBeNull();
        userType!.GetIndexes()
            .Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Email" }))
            .Should().BeTrue("User.(TenantId, Email) must have a composite unique index.");

        // 3. ApiKey: KeyHash must be unique
        var apiKeyType = _dbContext.Model.FindEntityType(typeof(ApiKey));
        apiKeyType.Should().NotBeNull();
        apiKeyType!.GetIndexes()
            .Any(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(ApiKey.KeyHash)))
            .Should().BeTrue("ApiKey.KeyHash must have a unique index.");

        // 4. RefreshToken: TokenHash must be unique
        var refreshTokenType = _dbContext.Model.FindEntityType(typeof(RefreshToken));
        refreshTokenType.Should().NotBeNull();
        refreshTokenType!.GetIndexes()
            .Any(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(RefreshToken.TokenHash)))
            .Should().BeTrue("RefreshToken.TokenHash must have a unique index.");

        // 5. Attempt: DeliveryId + AttemptNumber must be unique
        var attemptType = _dbContext.Model.FindEntityType(typeof(Attempt));
        attemptType.Should().NotBeNull();
        attemptType!.GetIndexes()
            .Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "DeliveryId", "AttemptNumber" }))
            .Should().BeTrue("Attempt.(DeliveryId, AttemptNumber) must have a composite unique index.");
    }

    [Fact]
    public async Task SaveChangesAsync_Automatically_Sets_Audit_Timestamps()
    {
        // Arrange
        var now = _dateTimeProvider.UtcNow;
        var app = HookBridge.Domain.Entities.Application.Create(_tenantId, "Auto Audit App", "Testing auditable timestamps", now).Value;

        // Act
        _dbContext.Applications.Add(app);
        await _dbContext.SaveChangesAsync();

        // Assert
        app.CreatedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(2));

        // Act 2: Update application
        app.Update("Auto Audit App Renamed", "Updated description", true, now.AddMinutes(5));
        await _dbContext.SaveChangesAsync();

        // Assert 2: UpdatedAt was updated
        app.UpdatedAt.Should().NotBeNull();
        app.UpdatedAt!.Value.Should().BeOnOrAfter(app.CreatedAt);
    }
}
