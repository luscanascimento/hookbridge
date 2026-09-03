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

public class AuditLogEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuditLogEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ControlPlaneActions_ShouldEmitAuditEntries_QueryableViaApi()
    {
        // Arrange
        var slug = $"audit-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Audit Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // 1. Create Application
        await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Audit App", null));

        // 2. Create API Key
        await _client.PostAsJsonAsync("/api/v1/api-keys", new CreateApiKeyCommand("Audit Key", ApiKeyScope.EventsIngest));

        // 3. Query Audit Logs
        var auditRes = await _client.GetAsync("/api/v1/audit-logs");
        auditRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedLogs = await auditRes.Content.ReadFromJsonAsync<PagedList<AuditEntryResponse>>(JsonOptions);
        pagedLogs.Should().NotBeNull();
        pagedLogs!.Items.Should().NotBeEmpty();

        var actions = pagedLogs.Items.Select(x => x.Action).ToList();
        actions.Should().Contain("Tenant.Registered");
        actions.Should().Contain("Application.Created");
        actions.Should().Contain("ApiKey.Created");
    }
}
