using HookBridge.Application.Abstractions;
using HookBridge.Application.Integration.DTOs;
using HookBridge.Domain.Common;

namespace HookBridge.IntegrationTests.Fixtures;

public class FakeEventFlowClient : IEventFlowClient
{
    public Task<Result<EventFlowIngestResponse>> IngestEventAsync(EventFlowIngestRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success(new EventFlowIngestResponse(
            request.EventId ?? Guid.NewGuid(),
            "Accepted",
            DateTimeOffset.UtcNow)));
    }

    public Task<Result<IReadOnlyList<DeadLetterMessageDto>>> PeekDlqAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DeadLetterMessageDto> list = new List<DeadLetterMessageDto>
        {
            new("dlq-msg-1", "eventflow.events", "payment.settled", "eventflow.events.dlq", "test-tenant", "payment.settled", "{}", null, null, "MaxRetriesExceeded", 3, DateTimeOffset.UtcNow)
        };
        return Task.FromResult(Result.Success(list));
    }

    public Task<Result<int>> ReplayDlqAsync(int maxCount = 50, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success(1));
    }

    public Task<Result<int>> PurgeDlqAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success(1));
    }
}
