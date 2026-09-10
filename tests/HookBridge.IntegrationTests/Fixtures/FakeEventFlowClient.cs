using HookBridge.Application.Abstractions;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;

namespace HookBridge.IntegrationTests.Fixtures;

public class FakeEventFlowClient : IEventFlowClient
{
    public Func<EventFlowIngestRequest, CancellationToken, Task<Result<EventFlowIngestResponse>>>? IngestHandler { get; set; }
    public Func<int, CancellationToken, Task<Result<IReadOnlyList<DeadLetterMessageDto>>>>? PeekDlqHandler { get; set; }
    public Func<int, CancellationToken, Task<Result<int>>>? ReplayDlqHandler { get; set; }
    public Func<CancellationToken, Task<Result<int>>>? PurgeDlqHandler { get; set; }

    public bool ShouldFailIngest { get; set; }
    public DomainError? IngestFailureError { get; set; }

    public bool ShouldFailDlq { get; set; }
    public DomainError? DlqFailureError { get; set; }

    public void Reset()
    {
        IngestHandler = null;
        PeekDlqHandler = null;
        ReplayDlqHandler = null;
        PurgeDlqHandler = null;
        ShouldFailIngest = false;
        IngestFailureError = null;
        ShouldFailDlq = false;
        DlqFailureError = null;
    }

    public Task<Result<EventFlowIngestResponse>> IngestEventAsync(EventFlowIngestRequest request, CancellationToken cancellationToken = default)
    {
        if (IngestHandler != null)
        {
            return IngestHandler(request, cancellationToken);
        }

        if (ShouldFailIngest)
        {
            return Task.FromResult(Result.Failure<EventFlowIngestResponse>(
                IngestFailureError ?? DomainError.Failure("EventFlow.ConnectionError", "Simulated broker outage: connection refused.")));
        }

        return Task.FromResult(Result.Success(new EventFlowIngestResponse(
            request.EventId ?? Guid.NewGuid(),
            "Accepted",
            DateTimeOffset.UtcNow)));
    }

    public Task<Result<IReadOnlyList<DeadLetterMessageDto>>> PeekDlqAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        if (PeekDlqHandler != null)
        {
            return PeekDlqHandler(count, cancellationToken);
        }

        if (ShouldFailDlq)
        {
            return Task.FromResult(Result.Failure<IReadOnlyList<DeadLetterMessageDto>>(
                DlqFailureError ?? DomainError.Failure("EventFlow.DlqPeekFailed", "Simulated DLQ broker connection failure.")));
        }

        IReadOnlyList<DeadLetterMessageDto> list = new List<DeadLetterMessageDto>
        {
            new("dlq-msg-1", "eventflow.events", "payment.settled", "eventflow.events.dlq", "test-tenant", "payment.settled", "{}", null, null, "MaxRetriesExceeded", 3, DateTimeOffset.UtcNow)
        };
        return Task.FromResult(Result.Success(list));
    }

    public Task<Result<int>> ReplayDlqAsync(int maxCount = 50, CancellationToken cancellationToken = default)
    {
        if (ReplayDlqHandler != null)
        {
            return ReplayDlqHandler(maxCount, cancellationToken);
        }

        if (ShouldFailDlq)
        {
            return Task.FromResult(Result.Failure<int>(
                DlqFailureError ?? DomainError.Failure("EventFlow.DlqReplayFailed", "Simulated DLQ replay connection failure.")));
        }

        return Task.FromResult(Result.Success(1));
    }

    public Task<Result<int>> PurgeDlqAsync(CancellationToken cancellationToken = default)
    {
        if (PurgeDlqHandler != null)
        {
            return PurgeDlqHandler(cancellationToken);
        }

        if (ShouldFailDlq)
        {
            return Task.FromResult(Result.Failure<int>(
                DlqFailureError ?? DomainError.Failure("EventFlow.DlqPurgeFailed", "Simulated DLQ purge connection failure.")));
        }

        return Task.FromResult(Result.Success(1));
    }
}
