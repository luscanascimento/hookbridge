using FluentAssertions;
using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.Auth.UseCases;
using HookBridge.Application.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.Security;

public sealed class RefreshTokenRotationAndFamilyRevocationTests : IDisposable
{
    private readonly HookBridgeDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly CurrentUser _currentUser;
    private readonly TokenService _tokenService;
    private readonly DateTimeProvider _dateTimeProvider;
    private readonly Guid _tenantId;
    private readonly Guid _userId;

    public RefreshTokenRotationAndFamilyRevocationTests()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        _userId = Guid.NewGuid();

        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId, "security-corp");

        _currentUser = new CurrentUser
        {
            UserId = _userId,
            Email = "admin@security.corp",
            Role = UserRole.TenantAdmin
        };

        _dbContext = new HookBridgeDbContext(options, _tenantContext);

        var jwtOptions = Options.Create(new JwtOptions
        {
            Key = "Super_Secret_Test_Key_At_Least_32_Bytes_Long_2026!",
            Issuer = "HookBridge.ControlPlane",
            Audience = "HookBridge.DeveloperPortal",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        });

        _tokenService = new TokenService(jwtOptions);
        _dateTimeProvider = new DateTimeProvider();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<(User User, Tenant Tenant)> SeedUserAndTenantAsync()
    {
        var tenant = Tenant.Create("security-corp", "Security Corp", _dateTimeProvider.UtcNow).Value;
        typeof(Tenant).GetProperty("Id")!.SetValue(tenant, _tenantId);

        var user = User.Create(_tenantId, "admin@security.corp", "hash_pw", UserRole.TenantAdmin, _dateTimeProvider.UtcNow).Value;
        typeof(User).GetProperty("Id")!.SetValue(user, _userId);

        _dbContext.Tenants.Add(tenant);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return (user, tenant);
    }

    private sealed class StubValidator : AbstractValidator<RefreshTokenCommand>
    {
    }

    [Fact]
    public async Task RefreshTokenUseCase_WhenValidTokenPresented_RotatesTokenAndRevokesPrevious()
    {
        // Arrange
        var (user, tenant) = await SeedUserAndTenantAsync();
        var initialTokens = _tokenService.GenerateTokens(user, tenant);

        var initialRefreshToken = RefreshToken.Create(
            user.Id, tenant.Id, initialTokens.RefreshTokenHash, _dateTimeProvider.UtcNow, TimeSpan.FromDays(7)).Value;

        _dbContext.RefreshTokens.Add(initialRefreshToken);
        await _dbContext.SaveChangesAsync();

        var useCase = new RefreshTokenUseCase(_dbContext, new StubValidator(), _tokenService, _dateTimeProvider);

        // Act
        var result = await useCase.ExecuteAsync(new RefreshTokenCommand(initialTokens.RefreshToken));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.Value.RefreshToken.Should().NotBe(initialTokens.RefreshToken);

        var oldTokenInDb = await _dbContext.RefreshTokens.FirstAsync(rt => rt.TokenHash == initialTokens.RefreshTokenHash);
        oldTokenInDb.RevokedAt.Should().NotBeNull();
        oldTokenInDb.ReplacedByTokenHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RefreshTokenUseCase_WhenRevokedTokenPresentedAgain_RevokesEntireFamilyAndReturnsCompromised()
    {
        // Arrange
        var (user, tenant) = await SeedUserAndTenantAsync();
        var initialTokens = _tokenService.GenerateTokens(user, tenant);

        var initialRefreshToken = RefreshToken.Create(
            user.Id, tenant.Id, initialTokens.RefreshTokenHash, _dateTimeProvider.UtcNow, TimeSpan.FromDays(7)).Value;

        _dbContext.RefreshTokens.Add(initialRefreshToken);
        await _dbContext.SaveChangesAsync();

        var useCase = new RefreshTokenUseCase(_dbContext, new StubValidator(), _tokenService, _dateTimeProvider);

        // Legitimate rotation (Token 1 -> Token 2)
        var firstRotationResult = await useCase.ExecuteAsync(new RefreshTokenCommand(initialTokens.RefreshToken));
        firstRotationResult.IsSuccess.Should().BeTrue();

        var activeTokenCountBeforeReuse = await _dbContext.RefreshTokens
            .CountAsync(rt => rt.UserId == user.Id && rt.RevokedAt == null);
        activeTokenCountBeforeReuse.Should().Be(1);

        // Act: Adversary presents the already-revoked Token 1 again!
        var reuseAttackResult = await useCase.ExecuteAsync(new RefreshTokenCommand(initialTokens.RefreshToken));

        // Assert: Attack is detected, token family is revoked
        reuseAttackResult.IsFailure.Should().BeTrue();
        reuseAttackResult.Error.Code.Should().Be("Auth.CompromisedToken");

        var activeTokenCountAfterReuse = await _dbContext.RefreshTokens
            .CountAsync(rt => rt.UserId == user.Id && rt.RevokedAt == null);
        activeTokenCountAfterReuse.Should().Be(0); // All tokens for this user terminated!
    }

    [Fact]
    public async Task LogoutUseCase_WhenCalled_RevokesTokensAndCreatesAuditRecord()
    {
        // Arrange
        var (user, tenant) = await SeedUserAndTenantAsync();
        var tokens = _tokenService.GenerateTokens(user, tenant);

        var refreshToken = RefreshToken.Create(
            user.Id, tenant.Id, tokens.RefreshTokenHash, _dateTimeProvider.UtcNow, TimeSpan.FromDays(7)).Value;

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync();

        var logoutUseCase = new LogoutUseCase(_dbContext, _tenantContext, _currentUser, _tokenService, _dateTimeProvider);

        // Act
        var result = await logoutUseCase.ExecuteAsync(new LogoutCommand(tokens.RefreshToken));

        // Assert
        result.IsSuccess.Should().BeTrue();

        var tokenInDb = await _dbContext.RefreshTokens.FirstAsync(rt => rt.TokenHash == tokens.RefreshTokenHash);
        tokenInDb.RevokedAt.Should().NotBeNull();
        tokenInDb.ReplacedByTokenHash.Should().Be("RevokedViaLogout");

        var auditLog = await _dbContext.AuditEntries.FirstOrDefaultAsync(a => a.Action == "User.LoggedOut");
        auditLog.Should().NotBeNull();
        auditLog!.UserId.Should().Be(_userId);
    }
}
