using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using HookBridge.Domain.Diagnostics;
using HookBridge.Infrastructure.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace HookBridge.UnitTests.Resilience;

public sealed class PollyResiliencePipelineTests
{
    [Fact]
    public async Task ExecuteAsync_WhenTargetReturns500_ShouldRetryConfiguredTimes()
    {
        // Arrange
        var attempts = 0;
        var options = new ResilienceOptions
        {
            MaxRetryAttempts = 3,
            BaseDelayMs = 10,
            MaxDelayMs = 50,
            UseJitter = false,
            CircuitBreakerMinimumThroughput = 100 // Prevent breaker from tripping in retry test
        };

        var provider = new HttpResiliencePipelineProvider(
            Options.Create(options),
            NullLogger<HttpResiliencePipelineProvider>.Instance);

        // Act
        var response = await provider.ExecuteAsync(ct =>
        {
            attempts++;
            if (attempts < 3)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        // Assert
        attempts.Should().Be(3);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTargetReturns429WithRetryAfterDelta_ShouldRespectHeader()
    {
        // Arrange
        var attempts = 0;
        var timestamps = new List<DateTimeOffset>();
        var options = new ResilienceOptions
        {
            MaxRetryAttempts = 2,
            BaseDelayMs = 10,
            MaxDelayMs = 1000,
            UseJitter = false,
            CircuitBreakerMinimumThroughput = 100
        };

        var provider = new HttpResiliencePipelineProvider(
            Options.Create(options),
            NullLogger<HttpResiliencePipelineProvider>.Instance);

        // Act
        var response = await provider.ExecuteAsync(ct =>
        {
            attempts++;
            timestamps.Add(DateTimeOffset.UtcNow);

            if (attempts == 1)
            {
                var resp429 = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                resp429.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(200));
                return Task.FromResult(resp429);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        // Assert
        attempts.Should().Be(2);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        timestamps.Count.Should().Be(2);

        var elapsedBetweenAttempts = timestamps[1] - timestamps[0];
        elapsedBetweenAttempts.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(180));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTargetConsistentlyFails_ShouldTripCircuitBreakerOpen()
    {
        // Arrange
        var options = new ResilienceOptions
        {
            MaxRetryAttempts = 1,
            BaseDelayMs = 10,
            CircuitBreakerMinimumThroughput = 2,
            CircuitBreakerSamplingDurationSeconds = 10,
            CircuitBreakerFailureRatio = 0.5,
            CircuitBreakerBreakDurationSeconds = 2
        };

        var provider = new HttpResiliencePipelineProvider(
            Options.Create(options),
            NullLogger<HttpResiliencePipelineProvider>.Instance);

        // Act: Make call1 which fails and retries once (total 2 failed attempts), tripping the circuit breaker open
        Func<Task> call1 = async () => await provider.ExecuteAsync(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));

        await call1();

        // Breaker should now be OPEN. Next call must immediately throw BrokenCircuitException
        Func<Task> callWhileOpen = async () => await provider.ExecuteAsync(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        // Assert
        await callWhileOpen.Should().ThrowAsync<BrokenCircuitException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenIndividualAttemptExceedsAttemptTimeout_ShouldThrowTimeoutRejectedException()
    {
        // Arrange
        var options = new ResilienceOptions
        {
            MaxRetryAttempts = 1,
            BaseDelayMs = 10,
            AttemptTimeoutSeconds = 1,
            TotalRequestTimeoutSeconds = 5,
            CircuitBreakerMinimumThroughput = 100
        };

        var provider = new HttpResiliencePipelineProvider(
            Options.Create(options),
            NullLogger<HttpResiliencePipelineProvider>.Instance);

        // Act
        Func<Task> action = async () => await provider.ExecuteAsync(async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Assert
        await action.Should().ThrowAsync<TimeoutRejectedException>();
    }
}
