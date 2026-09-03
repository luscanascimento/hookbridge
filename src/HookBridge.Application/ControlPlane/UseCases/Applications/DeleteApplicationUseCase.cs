using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Applications;

public sealed class DeleteApplicationUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeleteApplicationUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> ExecuteAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var app = await _dbContext.Applications
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.TenantId == tenantId, cancellationToken);

        if (app is null)
        {
            return Result.Failure(DomainError.NotFound(
                "Application.NotFound",
                $"Application with ID '{applicationId}' was not found."));
        }

        var now = _dateTimeProvider.UtcNow;
        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "Application.Deleted",
            "Application",
            app.Id.ToString(),
            JsonSerializer.Serialize(new { app.Name }),
            null,
            null,
            now).Value;

        _dbContext.Applications.Remove(app);
        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
