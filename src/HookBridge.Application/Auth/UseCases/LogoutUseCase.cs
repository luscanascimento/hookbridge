using HookBridge.Application.Abstractions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.Auth.UseCases;

/// <summary>
/// Handles secure session termination and refresh token invalidation.
/// </summary>
public sealed class LogoutUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITokenService _tokenService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public LogoutUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ITokenService tokenService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _tokenService = tokenService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<bool>> ExecuteAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;

        // 1. If an explicit refresh token is provided, hash and revoke it immediately
        if (!string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            var tokenHash = _tokenService.HashToken(command.RefreshToken);
            var token = await _dbContext.RefreshTokens
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

            if (token != null && token.RevokedAt == null)
            {
                token.Revoke(now, "RevokedViaLogout");
            }
        }

        // 2. If caller is an authenticated user, revoke all active refresh tokens for this user identity
        if (_currentUser.IsAuthenticated && _currentUser.UserId.HasValue)
        {
            var userId = _currentUser.UserId.Value;
            var activeTokens = await _dbContext.RefreshTokens
                .IgnoreQueryFilters()
                .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.Revoke(now, "RevokedViaLogout");
            }

            var tenantId = _tenantContext.TenantId ?? Guid.Empty;
            var auditResult = AuditEntry.Create(
                tenantId: tenantId,
                userId: userId,
                action: "User.LoggedOut",
                resourceType: "User",
                resourceId: userId.ToString(),
                detailsJson: "{\"event\":\"user_logout\"}",
                ipAddress: _currentUser.IpAddress,
                traceId: _currentUser.TraceId,
                timestamp: now);

            if (auditResult.IsSuccess)
            {
                _dbContext.AuditEntries.Add(auditResult.Value);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
