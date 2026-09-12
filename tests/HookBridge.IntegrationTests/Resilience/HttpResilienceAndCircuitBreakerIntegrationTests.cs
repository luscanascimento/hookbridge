using System.Net;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Infrastructure.Integration;
using HookBridge.Infrastructure.Resilience;
using HookBridge.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.IntegrationTests.Resilience;

public sealed class HttpResilienceAndCircuitBreakerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HttpResilienceAndCircuitBreakerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void HttpResiliencePipelineProvider_ShouldBeRegisteredAsSingleton()
    {
        // Arrange
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        // Act
        var provider1 = scope1.ServiceProvider.GetRequiredService<IHttpResiliencePipelineProvider>();
        var provider2 = scope2.ServiceProvider.GetRequiredService<IHttpResiliencePipelineProvider>();

        // Assert
        provider1.Should().NotBeNull();
        provider2.Should().NotBeNull();
        provider1.Should().BeSameAs(provider2);
        provider1.Pipeline.Should().NotBeNull();
    }

    [Fact]
    public async Task EventFlowClient_WithResiliencePipeline_TripsCircuitBreakerOnFailures()
    {
        // Arrange
        var requestCount = 0;
        var testHandler = new DelegatingMockHandler(req =>
        {
            Interlocked.Increment(ref requestCount);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        var testHttpClient = new HttpClient(testHandler)
        {
            BaseAddress = new Uri("http://eventflow.internal:5000")
        };

        var resilienceOptions = new ResilienceOptions
        {
            MaxRetryAttempts = 1,
            BaseDelayMs = 10,
            MaxDelayMs = 50,
            UseJitter = false,
            CircuitBreakerMinimumThroughput = 2,
            CircuitBreakerSamplingDurationSeconds = 10,
            CircuitBreakerFailureRatio = 0.5,
            CircuitBreakerBreakDurationSeconds = 5
        };

        var resilienceProvider = new HttpResiliencePipelineProvider(
            Options.Create(resilienceOptions),
            NullLogger<HttpResiliencePipelineProvider>.Instance);

        var client = new EventFlowClient(
            testHttpClient,
            Options.Create(new EventFlowOptions { ApiKey = "resilience-key", TimeoutSeconds = 5 }),
            resilienceProvider,
            NullLogger<EventFlowClient>.Instance);

        var payload = JsonDocument.Parse("{\"order\":\"100\"}").RootElement;
        var request = new EventFlowIngestRequest(
            Guid.NewGuid(), "order.created", 1, "test-source", DateTimeOffset.UtcNow,
            "corr-cb", null, "tenant-cb", "idemp-cb-1", payload, null);

        // Act: Call 1: fails + 1 retry = 2 failures. Throughput reaches 2, ratio 100% -> trips breaker!
        var call1 = await client.IngestEventAsync(request);

        // Call 2: breaker is now OPEN.
        var call2 = await client.IngestEventAsync(request with { IdempotencyKey = "idemp-cb-2" });

        // Assert
        call1.IsFailure.Should().BeTrue();
        call2.IsFailure.Should().BeTrue();
        call2.Error.Code.Should().Be("EventFlow.CircuitBroken");
    }

    private sealed class DelegatingMockHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public DelegatingMockHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
