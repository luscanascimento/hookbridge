using System.Net;
using System.Text.Json;
using FluentAssertions;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Infrastructure.Integration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.Integration;

public class EventFlowClientTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public HttpResponseMessage ResponseToReturn { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(ResponseToReturn);
        }
    }

    [Fact]
    public async Task IngestEventAsync_ShouldAttachHeadersAndReturnAccepted()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("{\"eventId\":\"01918a22-4a7b-7212-8e2b-7c3e1e9f1a01\",\"status\":\"Accepted\",\"ingestedAt\":\"2026-09-01T14:30:00Z\"}")
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var options = Options.Create(new EventFlowOptions { ApiKey = "test_api_key_123" });
        var client = new EventFlowClient(httpClient, options, NullLogger<EventFlowClient>.Instance);

        var payload = JsonDocument.Parse("{\"orderId\":\"123\"}").RootElement;
        var request = new EventFlowIngestRequest(
            Guid.NewGuid(), "order.created", 1, "test-source", DateTimeOffset.UtcNow,
            "corr_1", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            "tenant-1", "idemp-1", payload, null);

        // Act
        var result = await client.IngestEventAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Accepted");

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.GetValues("X-Api-Key").Should().Contain("test_api_key_123");
        handler.LastRequest.Headers.GetValues("traceparent").Should().Contain(request.TraceParent);
        handler.LastRequest.Headers.GetValues("X-Correlation-ID").Should().Contain("corr_1");
    }

    [Fact]
    public async Task IngestEventAsync_OnConflict_ShouldReturnDomainConflictError()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("{\"title\":\"Conflict\",\"detail\":\"Duplicate key\"}")
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var options = Options.Create(new EventFlowOptions { ApiKey = "test_api_key_123" });
        var client = new EventFlowClient(httpClient, options, NullLogger<EventFlowClient>.Instance);

        var payload = JsonDocument.Parse("{}").RootElement;
        var request = new EventFlowIngestRequest(
            Guid.NewGuid(), "order.created", 1, "test", DateTimeOffset.UtcNow,
            null, null, "tenant-1", "dup_key", payload, null);

        // Act
        var result = await client.IngestEventAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EventFlow.DuplicateEvent");
    }
}
