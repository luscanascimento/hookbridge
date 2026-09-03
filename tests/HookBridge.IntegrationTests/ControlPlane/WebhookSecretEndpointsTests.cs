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

public class WebhookSecretEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public WebhookSecretEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task WebhookSecret_RotationAndRevocation_ShouldSucceed()
    {
        // Arrange
        var slug = $"sec-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Sec Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Sec App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.github.com/hook"));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // 1. Get Secrets (Version 1 Active)
        var listRes1 = await _client.GetAsync($"/api/v1/endpoints/{ep!.Id}/secrets");
        listRes1.StatusCode.Should().Be(HttpStatusCode.OK);
        var secrets1 = await listRes1.Content.ReadFromJsonAsync<IReadOnlyList<WebhookSecretResponse>>(JsonOptions);
        secrets1.Should().ContainSingle();
        secrets1![0].Version.Should().Be(1);
        secrets1[0].Status.Should().Be(SecretStatus.Active);

        // 2. Rotate Secret (Generates Version 2, sets Version 1 to Rotating)
        var rotateRes = await _client.PostAsync($"/api/v1/endpoints/{ep.Id}/secrets/rotate", null);
        rotateRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var rotated = await rotateRes.Content.ReadFromJsonAsync<RotateSecretResponse>(JsonOptions);
        rotated!.Version.Should().Be(2);
        rotated.NewSecret.Should().StartWith("whsec_");

        // 3. Verify List shows 2 secrets (v2 Active, v1 Rotating)
        var listRes2 = await _client.GetAsync($"/api/v1/endpoints/{ep.Id}/secrets");
        var secrets2 = await listRes2.Content.ReadFromJsonAsync<IReadOnlyList<WebhookSecretResponse>>(JsonOptions);
        secrets2.Should().HaveCount(2);
        secrets2!.First(s => s.Version == 2).Status.Should().Be(SecretStatus.Active);
        secrets2.First(s => s.Version == 1).Status.Should().Be(SecretStatus.Rotating);

        // 4. Revoke Old Secret (v1)
        var v1SecretId = secrets2.First(s => s.Version == 1).Id;
        var revokeRes = await _client.DeleteAsync($"/api/v1/endpoints/{ep.Id}/secrets/{v1SecretId}");
        revokeRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 5. Verify v1 is Revoked
        var listRes3 = await _client.GetAsync($"/api/v1/endpoints/{ep.Id}/secrets");
        var secrets3 = await listRes3.Content.ReadFromJsonAsync<IReadOnlyList<WebhookSecretResponse>>(JsonOptions);
        secrets3!.First(s => s.Version == 1).Status.Should().Be(SecretStatus.Revoked);
    }
}
