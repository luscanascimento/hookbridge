using System.Net;
using System.Security.Claims;
using FluentAssertions;
using HookBridge.Api.Middleware;
using HookBridge.Application.Common;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.MultiTenancy;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Caching.Memory;

namespace HookBridge.UnitTests.Security;

public sealed class ApiKeyAuthenticationMiddlewareTests : IDisposable
{
    private readonly HookBridgeDbContext _dbContext;
    private readonly KeyGenerator _keyGenerator;
    private readonly DateTimeProvider _dateTimeProvider;
    private readonly Guid _tenantId;
    private readonly IMemoryCache _cache;

    public ApiKeyAuthenticationMiddlewareTests()
    {
        var options = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _tenantId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(_tenantId, "api-corp");

        _dbContext = new HookBridgeDbContext(options, tenantContext);
        _keyGenerator = new KeyGenerator();
        _dateTimeProvider = new DateTimeProvider();
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<(ApiKey Entity, string PlainKey)> SeedApiKeyAsync(bool revoked = false)
    {
        var (plainKey, keyPrefix, keyHash) = _keyGenerator.GenerateApiKey("live");
        var now = _dateTimeProvider.UtcNow;
        var apiKey = ApiKey.Create(_tenantId, "Test Ingestion Key", keyPrefix, keyHash, ApiKeyScope.EventsIngest, now).Value;

        if (revoked)
        {
            apiKey.Revoke(now);
        }

        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync();

        return (apiKey, plainKey);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoApiKeyProvided_PassesThroughWithoutAlteringUser()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context, _dbContext, _keyGenerator, _dateTimeProvider, _cache);

        // Assert
        nextCalled.Should().BeTrue();
        context.User.Identity?.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenValidXApiKeyProvided_SetsAuthenticatedDeveloperClaims()
    {
        // Arrange
        var (apiKey, plainKey) = await SeedApiKeyAsync();
        var nextCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = plainKey;

        // Act
        await middleware.InvokeAsync(context, _dbContext, _keyGenerator, _dateTimeProvider, _cache);

        // Assert
        nextCalled.Should().BeTrue();
        context.User.Identity.Should().NotBeNull();
        context.User.Identity!.IsAuthenticated.Should().BeTrue();
        context.User.Identity.AuthenticationType.Should().Be("ApiKey");
        context.User.FindFirst(ClaimTypes.Role)?.Value.Should().Be("Developer");
        context.User.FindFirst("tenant_id")?.Value.Should().Be(_tenantId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenValidBearerApiKeyProvided_SetsAuthenticatedClaims()
    {
        // Arrange
        var (apiKey, plainKey) = await SeedApiKeyAsync();
        var nextCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = $"Bearer {plainKey}";

        // Act
        await middleware.InvokeAsync(context, _dbContext, _keyGenerator, _dateTimeProvider, _cache);

        // Assert
        nextCalled.Should().BeTrue();
        context.User.Identity?.IsAuthenticated.Should().BeTrue();
        context.User.FindFirst("tenant_id")?.Value.Should().Be(_tenantId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenInvalidApiKeyProvided_TerminatesWith401Unauthorized()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "hb_live_000000000000000000000000";

        // Act
        await middleware.InvokeAsync(context, _dbContext, _keyGenerator, _dateTimeProvider, _cache);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        context.Response.ContentType.Should().Contain("problem+json");
    }

    [Fact]
    public async Task InvokeAsync_WhenRevokedApiKeyProvided_TerminatesWith401Unauthorized()
    {
        // Arrange
        var (apiKey, plainKey) = await SeedApiKeyAsync(revoked: true);
        var nextCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = plainKey;

        // Act
        await middleware.InvokeAsync(context, _dbContext, _keyGenerator, _dateTimeProvider, _cache);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
    }
}
