using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Domain.Enums;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.Api;

public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidParameters_ShouldReturn201Created_AndTokens()
    {
        // Arrange
        var slug = $"tenant-{Guid.NewGuid():N}";
        var command = new RegisterTenantCommand(
            TenantIdentifier: slug[..16],
            TenantName: "Integration Tenant",
            AdminEmail: $"{slug}@integration.test",
            AdminPassword: "SuperSecurePassword#2026");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrWhiteSpace();
        authResponse.RefreshToken.Should().NotBeNullOrWhiteSpace();
        authResponse.User.Email.Should().Be(command.AdminEmail);
        authResponse.User.Role.Should().Be(UserRole.TenantAdmin);
        authResponse.User.TenantIdentifier.Should().Be(command.TenantIdentifier);
    }

    [Fact]
    public async Task Register_WithDuplicateIdentifier_ShouldReturn409Conflict()
    {
        // Arrange
        var slug = $"dup-{Guid.NewGuid():N}"[..16];
        var command1 = new RegisterTenantCommand(
            TenantIdentifier: slug,
            TenantName: "Duplicate Test 1",
            AdminEmail: $"admin1_{slug}@dup.test",
            AdminPassword: "SecurePassword#2026");

        var command2 = new RegisterTenantCommand(
            TenantIdentifier: slug,
            TenantName: "Duplicate Test 2",
            AdminEmail: $"admin2_{slug}@dup.test",
            AdminPassword: "SecurePassword#2026");

        // Act
        var res1 = await _client.PostAsJsonAsync("/api/v1/auth/register", command1);
        var res2 = await _client.PostAsJsonAsync("/api/v1/auth/register", command2);

        // Assert
        res1.StatusCode.Should().Be(HttpStatusCode.Created);
        res2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturn200Ok_AndTokens()
    {
        // Arrange
        var slug = $"login-{Guid.NewGuid():N}"[..16];
        var email = $"admin_{slug}@login.test";
        var password = "ValidPassword#2026";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(slug, "Login Corp", email, password));

        var loginCommand = new LoginCommand(email, password, slug);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrWhiteSpace();
        authResponse.User.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturn401Unauthorized()
    {
        // Arrange
        var loginCommand = new LoginCommand("nonexistent@domain.com", "WrongPassword#123", null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_ShouldRotateTokens_AndRevokeOldToken()
    {
        // Arrange
        var slug = $"refresh-{Guid.NewGuid():N}"[..16];
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Refresh Corp", $"{slug}@refresh.test", "Password#2026"));

        var initialAuth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        // Act - First Refresh
        var refreshResponse1 = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenCommand(initialAuth!.RefreshToken));

        // Assert
        refreshResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotatedAuth = await refreshResponse1.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        rotatedAuth!.RefreshToken.Should().NotBe(initialAuth.RefreshToken);

        // Act - Replay old revoked token (should trigger compromise detection)
        var replayResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenCommand(initialAuth.RefreshToken));
        replayResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_WithoutAuthHeader_ShouldReturn401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ShouldReturn200Ok_AndUserProfile()
    {
        // Arrange
        var slug = $"me-{Guid.NewGuid():N}"[..16];
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Me Corp", $"{slug}@me.test", "Password#2026"));

        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);
        profile.Should().NotBeNull();
        profile!.Email.Should().Be($"{slug}@me.test");
        profile.TenantIdentifier.Should().Be(slug);
    }

    [Fact]
    public async Task InviteUser_AsTenantAdmin_ShouldReturn201Created()
    {
        // Arrange
        var slug = $"invite-{Guid.NewGuid():N}"[..16];
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Invite Corp", $"{slug}@admin.test", "Password#2026"));

        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        var inviteCommand = new InviteUserCommand(
            Email: $"developer@{slug}.test",
            Role: UserRole.Developer,
            InitialPassword: "DevPassword#2026");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/invite")
        {
            Content = JsonContent.Create(inviteCommand)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);
        createdUser!.Email.Should().Be(inviteCommand.Email);
        createdUser.Role.Should().Be(UserRole.Developer);
    }

    [Fact]
    public async Task InviteUser_AsViewer_ShouldReturn403Forbidden()
    {
        // Arrange: 1. Register TenantAdmin
        var slug = $"rbac-{Guid.NewGuid():N}"[..16];
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "RBAC Corp", $"{slug}@admin.test", "Password#2026"));
        var adminAuth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        // 2. Admin invites Viewer
        var inviteViewerRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/invite")
        {
            Content = JsonContent.Create(new InviteUserCommand($"viewer@{slug}.test", UserRole.Viewer, "ViewerPassword#2026"))
        };
        inviteViewerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        await _client.SendAsync(inviteViewerRequest);

        // 3. Login as Viewer
        var viewerLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand($"viewer@{slug}.test", "ViewerPassword#2026", slug));
        var viewerAuth = await viewerLogin.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        // 4. Viewer attempts to invite another user (Must be 403 Forbidden)
        var forbiddenInvite = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/invite")
        {
            Content = JsonContent.Create(new InviteUserCommand($"another@{slug}.test", UserRole.Developer, "Password#2026"))
        };
        forbiddenInvite.Headers.Authorization = new AuthenticationHeaderValue("Bearer", viewerAuth!.AccessToken);

        // Act
        var response = await _client.SendAsync(forbiddenInvite);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
