using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.Auth.UseCases;

public sealed class InviteUserUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly IValidator<InviteUserCommand> _validator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public InviteUserUseCase(
        IHookBridgeDbContext dbContext,
        IValidator<InviteUserCommand> validator,
        IPasswordHasher passwordHasher,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _validator = validator;
        _passwordHasher = passwordHasher;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<UserProfileResponse>> ExecuteAsync(InviteUserCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant || !_tenantContext.TenantId.HasValue)
        {
            return Result.Failure<UserProfileResponse>(DomainError.Unauthorized("Tenant.MissingContext", "Tenant context is required to invite users."));
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<UserProfileResponse>(DomainError.Validation(
                validation.Errors[0].PropertyName,
                validation.Errors[0].ErrorMessage));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        // 1. Check if email exists in current tenant
        bool emailInTenant = await _dbContext.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailInTenant)
        {
            return Result.Failure<UserProfileResponse>(DomainError.Conflict(
                "User.EmailAlreadyInTenant",
                $"A user with email '{normalizedEmail}' already belongs to this tenant."));
        }

        var now = _dateTimeProvider.UtcNow;
        var passwordHash = _passwordHasher.HashPassword(command.InitialPassword);

        // 2. Create User
        var userResult = User.Create(tenantId, normalizedEmail, passwordHash, command.Role, now);
        if (userResult.IsFailure)
        {
            return Result.Failure<UserProfileResponse>(userResult.Error);
        }

        var user = userResult.Value;

        // 3. Create Audit Entry
        var audit = AuditEntry.Create(
            tenantId,
            _currentUser.UserId,
            "User.Invited",
            "User",
            user.Id.ToString(),
            $"{{\"invitedEmail\":\"{user.Email}\",\"role\":\"{user.Role}\"}}",
            null,
            null,
            now).Value;

        _dbContext.Users.Add(user);
        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        return Result.Success(new UserProfileResponse(
            user.Id,
            tenantId,
            tenant?.Identifier ?? string.Empty,
            user.Email,
            user.Role,
            user.Status,
            user.LastLoginAt));
    }
}
