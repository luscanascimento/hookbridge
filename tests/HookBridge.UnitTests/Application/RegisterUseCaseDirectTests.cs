using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.Auth.UseCases;
using HookBridge.Application.Auth.Validators;
using HookBridge.Application.Common;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.Application;

public class RegisterUseCaseDirectTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldSucceed_AndSaveEntities()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new HookBridgeDbContext(options);
        var validator = new RegisterTenantValidator();
        var hasher = new PasswordHasher();
        var tokenService = new TokenService(Options.Create(new JwtOptions
        {
            SecretKey = "super_secret_test_key_must_be_256_bits_long_abcdefghijklmnopqrstuvwxyz!"
        }));
        var dt = new DateTimeProvider();

        var useCase = new RegisterTenantUseCase(db, validator, hasher, tokenService, dt);

        var command = new RegisterTenantCommand("acme-direct", "Acme Direct", "admin@acme-direct.com", "SecurePassword#2026");

        var result = await useCase.ExecuteAsync(command);

        result.IsSuccess.Should().BeTrue();
    }
}
