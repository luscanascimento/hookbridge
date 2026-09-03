using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Enums;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class ApplicationEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApplicationEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndGetTokenAsync(string? slugPrefix = null)
    {
        var slug = $"{slugPrefix ?? "app"}-{Guid.NewGuid():N}"[..16];
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "App Org", $"{slug}@test.com", "Password#2026"));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
    }

    [Fact]
    public async Task Application_CrudLifecycle_ShouldSucceed()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync("lifecycle");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Create Application
        var createCommand = new CreateApplicationCommand("Payment Service", "Processes online transactions");
        var createRes = await _client.PostAsJsonAsync("/api/v1/apps", createCommand);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdApp = await createRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);
        createdApp.Should().NotBeNull();
        createdApp!.Name.Should().Be("Payment Service");
        createdApp.Description.Should().Be("Processes online transactions");
        createdApp.IsActive.Should().BeTrue();

        // 2. Get Application by ID
        var getRes = await _client.GetAsync($"/api/v1/apps/{createdApp.Id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedApp = await getRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);
        fetchedApp!.Id.Should().Be(createdApp.Id);

        // 3. List Applications
        var listRes = await _client.GetAsync("/api/v1/apps");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var appsList = await listRes.Content.ReadFromJsonAsync<IReadOnlyList<ApplicationSummaryResponse>>(JsonOptions);
        appsList.Should().Contain(a => a.Id == createdApp.Id);

        // 4. Update Application
        var updateCommand = new UpdateApplicationCommand("Billing Service", "Updated Description", false);
        var updateRes = await _client.PutAsJsonAsync($"/api/v1/apps/{createdApp.Id}", updateCommand);
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedApp = await updateRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);
        updatedApp!.Name.Should().Be("Billing Service");
        updatedApp.IsActive.Should().BeFalse();

        // 5. Delete Application
        var deleteRes = await _client.DeleteAsync($"/api/v1/apps/{createdApp.Id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. Verify NotFound
        var verifyRes = await _client.GetAsync($"/api/v1/apps/{createdApp.Id}");
        verifyRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrossTenant_ApplicationAccess_ShouldReturn404NotFound()
    {
        // Arrange
        var tokenA = await RegisterAndGetTokenAsync("tenant-a");
        var tokenB = await RegisterAndGetTokenAsync("tenant-b");

        // Tenant A creates an Application
        var requestA = new HttpRequestMessage(HttpMethod.Post, "/api/v1/apps")
        {
            Content = JsonContent.Create(new CreateApplicationCommand("Tenant A Secret App", null))
        };
        requestA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var resA = await _client.SendAsync(requestA);
        var appA = await resA.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        // Tenant B attempts to read Tenant A's Application
        var requestB = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/apps/{appA!.Id}");
        requestB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var resB = await _client.SendAsync(requestB);

        // Assert
        resB.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
