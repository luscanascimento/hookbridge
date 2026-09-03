using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DomainApp = HookBridge.Domain.Entities.Application;

namespace HookBridge.Infrastructure.Persistence;

public sealed class HookBridgeDbContext : DbContext, IHookBridgeDbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly IDateTimeProvider? _dateTimeProvider;

    public HookBridgeDbContext(
        DbContextOptions<HookBridgeDbContext> options,
        ITenantContext? tenantContext = null,
        IDateTimeProvider? dateTimeProvider = null)
        : base(options)
    {
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public Guid CurrentTenantId => _tenantContext?.TenantId ?? Guid.Empty;
    public bool HasTenantFilter => _tenantContext?.HasTenant ?? false;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<DomainApp> Applications => Set<DomainApp>();
    public DbSet<Endpoint> Endpoints => Set<Endpoint>();
    public DbSet<WebhookSecret> WebhookSecrets => Set<WebhookSecret>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HookBridgeDbContext).Assembly);

        // Global Multi-Tenant Query Filter for all ITenantScoped entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(HookBridgeDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                    .MakeGenericMethod(entityType.ClrType);

                method?.Invoke(this, [modelBuilder]);
            }
        }

        // SQLite DateTimeOffset OrderBy Support for Integration Tests
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.ClrType.GetProperties()
                    .Where(p => p.PropertyType == typeof(DateTimeOffset) || p.PropertyType == typeof(DateTimeOffset?));

                foreach (var property in properties)
                {
                    modelBuilder
                        .Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasConversion(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToBinaryConverter());
                }
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            !HasTenantFilter || e.TenantId == CurrentTenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditableEntity.CreatedAt)).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditableEntity.UpdatedAt)).CurrentValue = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
