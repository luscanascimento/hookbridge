using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.Infrastructure.Integration;

public sealed partial class EventFlowClient : IEventFlowClient
{
    private readonly HttpClient _httpClient;
    private readonly EventFlowOptions _options;
    private readonly ILogger<EventFlowClient> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public EventFlowClient(
        HttpClient httpClient,
        IOptions<EventFlowOptions> options,
        ILogger<EventFlowClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;

        if (_httpClient.BaseAddress == null && !string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "EventFlow Ingestion accepted: EventId={EventId}, EventType={EventType}, TenantId={TenantId}")]
    private static partial void LogEventIngested(ILogger logger, Guid eventId, string eventType, string tenantId);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "EventFlow Ingestion failed: StatusCode={StatusCode}, Error={Error}")]
    private static partial void LogEventIngestFailed(ILogger logger, int statusCode, string error);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error, Message = "EventFlow Ingestion request threw exception.")]
    private static partial void LogEventIngestException(ILogger logger, Exception ex);

    public async Task<Result<EventFlowIngestResponse>> IngestEventAsync(EventFlowIngestRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/events")
            {
                Content = JsonContent.Create(request)
            };

            AddSecurityAndTracingHeaders(httpRequest, request.TraceParent, request.CorrelationId);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadFromJsonAsync<EventFlowIngestResponse>(JsonOpts, cancellationToken);
                if (responseContent != null)
                {
                    LogEventIngested(_logger, responseContent.EventId, request.EventType, request.TenantId);
                    return Result.Success(responseContent);
                }
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            LogEventIngestFailed(_logger, (int)response.StatusCode, errorBody);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                return Result.Failure<EventFlowIngestResponse>(DomainError.Conflict(
                    "EventFlow.DuplicateEvent",
                    $"Duplicate event or idempotency key '{request.IdempotencyKey}': {errorBody}"));
            }

            return Result.Failure<EventFlowIngestResponse>(DomainError.Validation(
                "EventFlow.IngestFailed",
                $"EventFlow ingestion rejected with status code {(int)response.StatusCode}: {errorBody}"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogEventIngestException(_logger, ex);
            return Result.Failure<EventFlowIngestResponse>(DomainError.Failure(
                "EventFlow.ConnectionError",
                $"Could not reach EventFlow Data Plane at '{_options.BaseUrl}': {ex.Message}"));
        }
    }

    public async Task<Result<IReadOnlyList<DeadLetterMessageDto>>> PeekDlqAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/dlq?count={count}");
            AddSecurityAndTracingHeaders(httpRequest, null, null);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DlqPeekResponseInternal>(JsonOpts, cancellationToken);
                return Result.Success<IReadOnlyList<DeadLetterMessageDto>>(result?.Messages ?? Array.Empty<DeadLetterMessageDto>());
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            return Result.Failure<IReadOnlyList<DeadLetterMessageDto>>(DomainError.Failure(
                "EventFlow.DlqPeekFailed",
                $"Failed to peek DLQ: {error}"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure<IReadOnlyList<DeadLetterMessageDto>>(DomainError.Failure(
                "EventFlow.ConnectionError",
                $"Could not reach EventFlow DLQ at '{_options.BaseUrl}': {ex.Message}"));
        }
    }

    public async Task<Result<int>> ReplayDlqAsync(int maxCount = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/dlq/replay?maxCount={maxCount}");
            AddSecurityAndTracingHeaders(httpRequest, null, null);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DlqReplayResponseInternal>(JsonOpts, cancellationToken);
                return Result.Success(result?.ReplayedCount ?? 0);
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            return Result.Failure<int>(DomainError.Failure(
                "EventFlow.DlqReplayFailed",
                $"Failed to replay DLQ: {error}"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure<int>(DomainError.Failure(
                "EventFlow.ConnectionError",
                $"Could not reach EventFlow DLQ at '{_options.BaseUrl}': {ex.Message}"));
        }
    }

    public async Task<Result<int>> PurgeDlqAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/dlq");
            AddSecurityAndTracingHeaders(httpRequest, null, null);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DlqPurgeResponseInternal>(JsonOpts, cancellationToken);
                return Result.Success(result?.PurgedCount ?? 0);
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            return Result.Failure<int>(DomainError.Failure(
                "EventFlow.DlqPurgeFailed",
                $"Failed to purge DLQ: {error}"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure<int>(DomainError.Failure(
                "EventFlow.ConnectionError",
                $"Could not reach EventFlow DLQ at '{_options.BaseUrl}': {ex.Message}"));
        }
    }

    private void AddSecurityAndTracingHeaders(HttpRequestMessage request, string? traceParent, string? correlationId)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", _options.ApiKey);
        }

        var activeTraceParent = traceParent ?? Activity.Current?.Id;
        if (!string.IsNullOrWhiteSpace(activeTraceParent))
        {
            request.Headers.TryAddWithoutValidation("traceparent", activeTraceParent);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
        }
    }

    private sealed record DlqPeekResponseInternal(int Count, IReadOnlyList<DeadLetterMessageDto> Messages);
    private sealed record DlqReplayResponseInternal(int ReplayedCount, string Status);
    private sealed record DlqPurgeResponseInternal(int PurgedCount, string Status);
}
