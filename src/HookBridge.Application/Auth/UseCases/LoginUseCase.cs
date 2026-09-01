using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.Auth.UseCases;

public sealed class LoginUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly IValidator<LoginCommand> _validator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public LoginUseCase(
        IHookBridgeDbContext dbContext,
        IValidator<LoginCommand> validator,
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

    public async Task<Result<AuthResponse>> ExecuteAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AuthResponse>(DomainError.Validation(
                validation.Errors[0].PropertyName,
                validation.Errors[0].ErrorMessage));
        }

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        // 1. Fetch user ignoring query filters initially to locate tenant
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        // Mitigate account enumeration & timing attacks: verify dummy password if user not found
        if (user == null || user.Status != UserStatus.Active)
        {
            _passwordHasher.VerifyPassword(command.Password, "dummy_hash_to_equalize_timing_characteristics_pbkdf2");
            return Result.Failure<AuthResponse>(DomainError.Unauthorized("Auth.InvalidCredentials", "Invalid email or password."));
        }

        // 2. Verify Tenant is active
        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId, cancellationToken);

        if (tenant == null || tenant.Status != TenantStatus.Active)
        {
            return Result.Failure<AuthResponse>(DomainError.Forbidden("Tenant.Suspended", "The associated tenant account is suspended or disabled."));
        }

        // 3. Optional tenant identifier verification
        if (!string.IsNullOrWhiteSpace(command.TenantIdentifier) &&
            !string.Equals(tenant.Identifier, command.TenantIdentifier.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<AuthResponse>(DomainError.Unauthorized("Auth.InvalidCredentials", "Invalid email or password."));
        }

        // 4. Verify password
        bool passwordValid = _passwordHasher.VerifyPassword(command.Password, user.PasswordHash);
        if (!passwordValid)
        {
            return Result.Failure<AuthResponse>(DomainError.Unauthorized("Auth.InvalidCredentials", "Invalid email or password."));
        }

        var now = _dateTimeProvider.UtcNow;
        user.RecordLogin(now);

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

        _dbContext.RefreshTokens.Add(refreshTokenResult.Value);
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
