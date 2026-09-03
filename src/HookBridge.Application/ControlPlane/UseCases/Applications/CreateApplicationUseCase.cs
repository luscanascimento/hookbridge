using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DomainApp = HookBridge.Domain.Entities.Application;

namespace HookBridge.Application.ControlPlane.UseCases.Applications;

public sealed class CreateApplicationUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateApplicationCommand> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateApplicationUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IValidator<CreateApplicationCommand> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ApplicationResponse>> ExecuteAsync(CreateApplicationCommand command, CancellationToken cancellationToken = default)
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

        var trimmedName = command.Name.Trim();
        var exists = await _dbContext.Applications
            .AnyAsync(a => a.TenantId == tenantId && a.Name == trimmedName, cancellationToken);

        if (exists)
        {
            return Result.Failure<ApplicationResponse>(DomainError.Conflict(
                "Application.NameInUse",
                $"An application named '{trimmedName}' already exists within this tenant."));
        }

        var now = _dateTimeProvider.UtcNow;
        var appResult = DomainApp.Create(tenantId, trimmedName, command.Description, now);
        if (appResult.IsFailure)
        {
            return Result.Failure<ApplicationResponse>(appResult.Error);
        }

        var app = appResult.Value;

        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "Application.Created",
            "Application",
            app.Id.ToString(),
            JsonSerializer.Serialize(new { app.Name, app.Description }),
            null,
            null,
            now).Value;

        _dbContext.Applications.Add(app);
        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ApplicationResponse(
            app.Id,
            app.TenantId,
            app.Name,
            app.Description,
            app.IsActive,
            0,
            app.CreatedAt,
            app.UpdatedAt));
    }
}
