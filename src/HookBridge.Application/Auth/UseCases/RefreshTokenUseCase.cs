using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.Auth.UseCases;

public sealed class RefreshTokenUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly IValidator<RefreshTokenCommand> _validator;
    private readonly ITokenService _tokenService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RefreshTokenUseCase(
        IHookBridgeDbContext dbContext,
        IValidator<RefreshTokenCommand> validator,
        ITokenService tokenService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _validator = validator;
        _tokenService = tokenService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<AuthResponse>> ExecuteAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AuthResponse>(DomainError.Validation(
                validation.Errors[0].PropertyName,
                validation.Errors[0].ErrorMessage));
        }

        var tokenHash = _tokenService.HashToken(command.RefreshToken);

        // 1. Locate refresh token
        var refreshToken = await _dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken == null)
        {
            return Result.Failure<AuthResponse>(DomainError.Unauthorized("Auth.InvalidRefreshToken", "Invalid or unrecognized refresh token."));
        }

        var now = _dateTimeProvider.UtcNow;

        // 2. Compromise Detection: if an already revoked token is used, revoke all user tokens
        if (refreshToken.RevokedAt != null)
        {
            var userTokens = await _dbContext.RefreshTokens
                .IgnoreQueryFilters()
                .Where(rt => rt.UserId == refreshToken.UserId && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in userTokens)
            {
                token.Revoke(now, "RevokedDueToCompromisedTokenReuse");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Failure<AuthResponse>(DomainError.Unauthorized("Auth.CompromisedToken", "Refresh token has been revoked. All active sessions terminated."));
        }

        if (refreshToken.ExpiresAt <= now)
        {
            return Result.Failure<AuthResponse>(DomainError.Unauthorized("Auth.ExpiredRefreshToken", "Refresh token has expired."));
        }

        // 3. Load User and Tenant
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == refreshToken.UserId, cancellationToken);

        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == refreshToken.TenantId, cancellationToken);

        if (user == null || user.Status != UserStatus.Active || tenant == null || tenant.Status != TenantStatus.Active)
        {
            return Result.Failure<AuthResponse>(DomainError.Unauthorized("Auth.AccountInactive", "User or Tenant account is inactive."));
        }

        // 4. Rotate Refresh Token
        var newTokens = _tokenService.GenerateTokens(user, tenant);
        refreshToken.Revoke(now, newTokens.RefreshTokenHash);

        var newRefreshTokenResult = RefreshToken.Create(
            user.Id,
            tenant.Id,
            newTokens.RefreshTokenHash,
            now,
            TimeSpan.FromDays(7));

        if (newRefreshTokenResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(newRefreshTokenResult.Error);
        }

        _dbContext.RefreshTokens.Add(newRefreshTokenResult.Value);
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
            newTokens.AccessToken,
            newTokens.RefreshToken,
            newTokens.ExpiresInSeconds,
            "Bearer",
            profile));
    }
}
