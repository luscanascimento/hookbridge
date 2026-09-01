using HookBridge.Application.Abstractions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.Auth.UseCases;

public sealed class GetCurrentUserUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;

    public GetCurrentUserUseCase(
        IHookBridgeDbContext dbContext,
        ICurrentUser currentUser,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Result<UserProfileResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
        {
            return Result.Failure<UserProfileResponse>(DomainError.Unauthorized("Auth.Unauthenticated", "User is not authenticated."));
        }

        var userId = _currentUser.UserId.Value;

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            return Result.Failure<UserProfileResponse>(DomainError.NotFound("User.NotFound", "User record not found."));
        }

        var tenantId = _tenantContext.TenantId ?? user.TenantId;
        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

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
