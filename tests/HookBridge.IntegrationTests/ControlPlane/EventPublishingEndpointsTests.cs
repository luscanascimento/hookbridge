using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.Integration.DTOs;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class EventPublishingEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EventPublishingEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PublishEvent_ShouldSucceed_AndScheduleDeliveries()
    {
        // Arrange
        var slug = $"pub-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Pub Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Pub App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.github.com/hook", "Order Webhooks", 600, 15, new List<string> { "payment.*" }));
        await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        var payload = JsonDocument.Parse("{\"paymentId\":\"pay_99812\",\"amount\":250.75}").RootElement;
        var publishCommand = new PublishEventCommand("payment.settled.v1", payload, "idemp_publish_test_1");

        // Act
        var pubRes = await _client.PostAsJsonAsync("/api/v1/events", publishCommand);

        // Assert
        pubRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var pub = await pubRes.Content.ReadFromJsonAsync<PublishEventResponse>(JsonOptions);
        pub.Should().NotBeNull();
        pub!.Status.Should().Be("Accepted");
        pub.EventType.Should().Be("payment.settled.v1");
        pub.DeliveriesScheduled.Should().Be(1);
        pub.TraceParent.Should().NotBeNullOrWhiteSpace();
    }
}
