using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DomainApp = HookBridge.Domain.Entities.Application;

namespace HookBridge.Application.Abstractions;

/// <summary>
/// Abstraction for database persistence operations across HookBridge entities.
/// </summary>
public interface IHookBridgeDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<DomainApp> Applications { get; }
    DbSet<Endpoint> Endpoints { get; }
    DbSet<WebhookSecret> WebhookSecrets { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<ApiKey> ApiKeys { get; }
    DbSet<Delivery> Deliveries { get; }
    DbSet<Attempt> Attempts { get; }
    DbSet<AuditEntry> AuditEntries { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
