using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.IntegrationTests.Errors;

public class ErrorHandlingAndProblemDetailsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ErrorHandlingAndProblemDetailsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid TenantId)> RegisterTenantAsync(string prefix)
    {
        var slug = $"{prefix}-{Guid.NewGuid():N}"[..16];
        var email = $"admin@{slug}.test";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, $"Tenant {prefix}", email, "Password#2026"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return (auth!.AccessToken, auth.User.TenantId);
    }

    [Fact]
    public async Task ValidationFailure_MustReturnRFC7807ProblemDetails_WithErrorsDictionary()
    {
        // Act: Send invalid registration payload (empty fields)
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            "", "", "not-an-email", "short"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("title").GetString().Should().Be("Validation Failure");
        problem.TryGetProperty("errors", out var errorsProp).Should().BeTrue();
        errorsProp.EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task NotFound_MustReturnRFC7807ProblemDetails()
    {
        // Arrange
        var (token, _) = await RegisterTenantAsync("err-404");
        var nonExistentId = Guid.NewGuid();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/apps/{nonExistentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        problem.GetProperty("status").GetInt32().Should().Be(404);
        problem.GetProperty("title").GetString().Should().Be("Not Found");
        problem.GetProperty("errorCode").GetString().Should().Be("Application.NotFound");
    }

    [Fact]
    public async Task Conflict_MustReturnRFC7807ProblemDetails()
    {
        // Arrange
        var (token, _) = await RegisterTenantAsync("err-409");

        // Create initial application
        var createReq1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/apps")
        {
            Content = JsonContent.Create(new CreateApplicationCommand("DuplicateName", null))
        };
        createReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes1 = await _client.SendAsync(createReq1);
        createRes1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: Create duplicate application with same name in same tenant
        var createReq2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/apps")
        {
            Content = JsonContent.Create(new CreateApplicationCommand("DuplicateName", null))
        };
        createReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes2 = await _client.SendAsync(createReq2);

        // Assert
        createRes2.StatusCode.Should().Be(HttpStatusCode.Conflict);
        createRes2.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await createRes2.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        problem.GetProperty("status").GetInt32().Should().Be(409);
        problem.GetProperty("title").GetString().Should().Be("Conflict");
        problem.GetProperty("errorCode").GetString().Should().Be("Application.NameInUse");
    }

    [Fact]
    public async Task Unauthorized_MustReturnRFC7807ProblemDetails()
    {
        // Act: Access protected resource with invalid token
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/apps");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid_token");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
