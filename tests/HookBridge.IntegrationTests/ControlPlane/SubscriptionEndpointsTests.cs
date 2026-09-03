using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class SubscriptionEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SubscriptionEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Subscription_Lifecycle_ShouldSucceed()
    {
        // Arrange
        var slug = $"sub-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Sub Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Sub App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.github.com/hook"));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        // 1. Create Subscription
        var createSubRes = await _client.PostAsJsonAsync($"/api/v1/endpoints/{ep!.Id}/subscriptions", new CreateSubscriptionCommand("order.created"));
        createSubRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var sub = await createSubRes.Content.ReadFromJsonAsync<SubscriptionResponse>(JsonOptions);
        sub!.EventTypePattern.Should().Be("order.created");

        // 2. Duplicate Subscription should return 409 Conflict
        var dupRes = await _client.PostAsJsonAsync($"/api/v1/endpoints/{ep.Id}/subscriptions", new CreateSubscriptionCommand("order.created"));
        dupRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 3. List Subscriptions
        var listRes = await _client.GetAsync($"/api/v1/endpoints/{ep.Id}/subscriptions");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var subs = await listRes.Content.ReadFromJsonAsync<IReadOnlyList<SubscriptionResponse>>(JsonOptions);
        subs.Should().ContainSingle(s => s.EventTypePattern == "order.created");

        // 4. Delete Subscription
        var delRes = await _client.DeleteAsync($"/api/v1/endpoints/{ep.Id}/subscriptions/{sub.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
