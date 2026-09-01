using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.Auth.UseCases;

public sealed class RegisterTenantUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly IValidator<RegisterTenantCommand> _validator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RegisterTenantUseCase(
        IHookBridgeDbContext dbContext,
        IValidator<RegisterTenantCommand> validator,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _validator = validator;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<AuthResponse>> ExecuteAsync(RegisterTenantCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AuthResponse>(DomainError.Validation(
                validation.Errors[0].PropertyName,
                validation.Errors[0].ErrorMessage));
        }

        var normalizedSlug = command.TenantIdentifier.Trim().ToLowerInvariant();
        var normalizedEmail = command.AdminEmail.Trim().ToLowerInvariant();

        // 1. Check Tenant uniqueness ignoring tenant query filter
        bool tenantExists = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Identifier == normalizedSlug, cancellationToken);

        if (tenantExists)
        {
            return Result.Failure<AuthResponse>(DomainError.Conflict(
                "Tenant.IdentifierInUse",
                $"The tenant identifier '{normalizedSlug}' is already in use."));
        }

        // 2. Check Admin Email uniqueness across system
        bool emailExists = await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            return Result.Failure<AuthResponse>(DomainError.Conflict(
                "User.EmailInUse",
                $"An account with email '{normalizedEmail}' already exists."));
        }

        var now = _dateTimeProvider.UtcNow;

        // 3. Create Tenant
        var tenantResult = Tenant.Create(normalizedSlug, command.TenantName, now);
        if (tenantResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(tenantResult.Error);
        }

        var tenant = tenantResult.Value;

        // 4. Create Admin User
        var passwordHash = _passwordHasher.HashPassword(command.AdminPassword);
        var userResult = User.Create(tenant.Id, normalizedEmail, passwordHash, UserRole.TenantAdmin, now);
        if (userResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(userResult.Error);
        }

        var user = userResult.Value;

        // 5. Generate Auth Tokens
        var tokens = _tokenService.GenerateTokens(user, tenant);
        var refreshTokenResult = RefreshToken.Create(
            user.Id,
            tenant.Id,
            tokens.RefreshTokenHash,
            now,
            TimeSpan.FromDays(7));

        if (refreshTokenResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(refreshTokenResult.Error);
        }

        // 6. Create Audit Entry
        var audit = AuditEntry.Create(
            tenant.Id,
            user.Id,
            "Tenant.Registered",
            "Tenant",
            tenant.Id.ToString(),
            $"{{\"tenantIdentifier\":\"{tenant.Identifier}\",\"adminEmail\":\"{user.Email}\"}}",
            null,
            null,
            now).Value;

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        _dbContext.RefreshTokens.Add(refreshTokenResult.Value);
        _dbContext.AuditEntries.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var profile = new UserProfileResponse(
            user.Id,
            tenant.Id,
            tenant.Identifier,
            user.Email,
            user.Role,
            user.Status,
            user.LastLoginAt);

        return Result.Success(new AuthResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresInSeconds,
            "Bearer",
            profile));
    }
}
