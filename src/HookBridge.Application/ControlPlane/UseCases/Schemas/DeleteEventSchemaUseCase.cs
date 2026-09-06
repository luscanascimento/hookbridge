using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Schemas;

public sealed class DeleteEventSchemaUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeleteEventSchemaUseCase(
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

    public async Task<Result> ExecuteAsync(
        Guid schemaId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var schema = await _dbContext.EventSchemas
            .FirstOrDefaultAsync(s => s.Id == schemaId && s.TenantId == tenantId, cancellationToken);

        if (schema == null)
        {
            return Result.Failure(
                DomainError.NotFound("EventSchema.NotFound", $"Event schema with ID '{schemaId}' was not found."));
        }

        var now = _dateTimeProvider.UtcNow;
        var auditResult = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "EventSchema.Deleted",
            "EventSchema",
            schema.Id.ToString(),
            $"{{\"name\":\"{schema.Name}\",\"eventType\":\"{schema.EventType}\"}}",
            null,
            null,
            now);

        _dbContext.EventSchemas.Remove(schema);
        if (auditResult.IsSuccess)
        {
            await _dbContext.AuditEntries.AddAsync(auditResult.Value, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
