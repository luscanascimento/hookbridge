using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Endpoints;

public sealed class DeleteEndpointUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeleteEndpointUseCase(
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

    public async Task<Result> ExecuteAsync(Guid endpointId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var endpoint = await _dbContext.Endpoints
            .FirstOrDefaultAsync(e => e.Id == endpointId && e.TenantId == tenantId, cancellationToken);

        if (endpoint is null)
        {
            return Result.Failure(DomainError.NotFound(
                "Endpoint.NotFound",
                $"Endpoint with ID '{endpointId}' was not found."));
        }

        var now = _dateTimeProvider.UtcNow;
        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "Endpoint.Deleted",
            "Endpoint",
            endpoint.Id.ToString(),
            JsonSerializer.Serialize(new { endpoint.TargetUrl, endpoint.ApplicationId }),
            null,
            null,
            now).Value;

        _dbContext.Endpoints.Remove(endpoint);
        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
