using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Applications;

public sealed class UpdateApplicationUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<UpdateApplicationCommand> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateApplicationUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IValidator<UpdateApplicationCommand> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ApplicationResponse>> ExecuteAsync(Guid applicationId, UpdateApplicationCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<ApplicationResponse>(DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ApplicationResponse>(DomainError.Validation(validation.Errors[0].PropertyName, validation.Errors[0].ErrorMessage));
        }

        var app = await _dbContext.Applications
            .Include(a => a.Endpoints)
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.TenantId == tenantId, cancellationToken);

        if (app is null)
        {
            return Result.Failure<ApplicationResponse>(DomainError.NotFound(
                "Application.NotFound",
                $"Application with ID '{applicationId}' was not found."));
        }

        var trimmedName = command.Name.Trim();
        if (!string.Equals(app.Name, trimmedName, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _dbContext.Applications
                .AnyAsync(a => a.TenantId == tenantId && a.Id != applicationId && a.Name == trimmedName, cancellationToken);

            if (exists)
            {
                return Result.Failure<ApplicationResponse>(DomainError.Conflict(
                    "Application.NameInUse",
                    $"An application named '{trimmedName}' already exists within this tenant."));
            }
        }

        var now = _dateTimeProvider.UtcNow;
        app.Update(trimmedName, command.Description, command.IsActive, now);

        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "Application.Updated",
            "Application",
            app.Id.ToString(),
            JsonSerializer.Serialize(new { app.Name, app.Description, app.IsActive }),
            null,
            null,
            now).Value;

        _dbContext.AuditEntries.Add(audit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ApplicationResponse(
            app.Id,
            app.TenantId,
            app.Name,
            app.Description,
            app.IsActive,
            app.Endpoints.Count,
            app.CreatedAt,
            app.UpdatedAt));
    }
}
