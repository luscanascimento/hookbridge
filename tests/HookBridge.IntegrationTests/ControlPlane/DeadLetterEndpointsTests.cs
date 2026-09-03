using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.Integration.DTOs;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class DeadLetterEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DeadLetterEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeadLetter_PeekReplayPurge_ShouldSucceed()
    {
        // Arrange
        var slug = $"dlq-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "DLQ Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // 1. Peek DLQ
        var peekRes = await _client.GetAsync("/api/v1/dlq?count=5");
        peekRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var peek = await peekRes.Content.ReadFromJsonAsync<DeadLetterListResponse>(JsonOptions);
        peek.Should().NotBeNull();
        peek!.Count.Should().BeGreaterThanOrEqualTo(1);

        // 2. Replay DLQ
        var replayRes = await _client.PostAsync("/api/v1/dlq/replay?maxCount=10", null);
        replayRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var replay = await replayRes.Content.ReadFromJsonAsync<DeadLetterReplayResponse>(JsonOptions);
        replay.Should().NotBeNull();
        replay!.ReplayedCount.Should().Be(1);

        // 3. Purge DLQ
        var purgeRes = await _client.DeleteAsync("/api/v1/dlq");
        purgeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var purge = await purgeRes.Content.ReadFromJsonAsync<DeadLetterPurgeResponse>(JsonOptions);
        purge.Should().NotBeNull();
        purge!.PurgedCount.Should().Be(1);
    }
}
