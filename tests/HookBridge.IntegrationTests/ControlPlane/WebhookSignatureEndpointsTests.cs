using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.IntegrationTests.Fixtures;

namespace HookBridge.IntegrationTests.ControlPlane;

public class WebhookSignatureEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public WebhookSignatureEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task WebhookSignature_GenerateAndVerify_ApiLifecycle()
    {
        // Arrange
        var slug = $"sig-{Guid.NewGuid():N}"[..16];
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterTenantCommand(
            slug, "Sig Org", $"{slug}@test.com", "Password#2026"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var appRes = await _client.PostAsJsonAsync("/api/v1/apps", new CreateApplicationCommand("Sig App", null));
        var app = await appRes.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);

        var epRes = await _client.PostAsJsonAsync("/api/v1/endpoints", new CreateEndpointCommand(
            app!.Id, "https://api.github.com/hook"));
        var ep = await epRes.Content.ReadFromJsonAsync<EndpointCreatedResponse>(JsonOptions);

        var payload = "{\"event\":\"charge.refunded\",\"amount\":1500}";

        // 1. Generate Signature
        var genRes = await _client.PostAsJsonAsync(
            $"/api/v1/endpoints/{ep!.Id}/signatures/generate",
            new GenerateSignatureCommand(payload));

        genRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var gen = await genRes.Content.ReadFromJsonAsync<GenerateSignatureResponse>(JsonOptions);
        gen.Should().NotBeNull();
        gen!.SignatureHeader.Should().StartWith("t=");
        gen.Signatures.Should().ContainSingle();

        // 2. Verify Valid Signature
        var verifyRes = await _client.PostAsJsonAsync(
            $"/api/v1/endpoints/{ep.Id}/signatures/verify",
            new VerifySignatureCommand(payload, gen.SignatureHeader));

        verifyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var verify = await verifyRes.Content.ReadFromJsonAsync<VerifySignatureResponse>(JsonOptions);
        verify!.IsValid.Should().BeTrue();

        // 3. Verify Tampered Payload fails
        var tamperedPayload = "{\"event\":\"charge.refunded\",\"amount\":999999}";
        var verifyTamperedRes = await _client.PostAsJsonAsync(
            $"/api/v1/endpoints/{ep.Id}/signatures/verify",
            new VerifySignatureCommand(tamperedPayload, gen.SignatureHeader));

        verifyTamperedRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var verifyTampered = await verifyTamperedRes.Content.ReadFromJsonAsync<VerifySignatureResponse>(JsonOptions);
        verifyTampered!.IsValid.Should().BeFalse();
    }
}
