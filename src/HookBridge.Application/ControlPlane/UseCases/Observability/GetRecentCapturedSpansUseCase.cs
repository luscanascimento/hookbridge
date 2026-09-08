using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;

namespace HookBridge.Application.ControlPlane.UseCases.Observability;

public sealed class GetRecentCapturedSpansUseCase
{
    private readonly ITelemetryBuffer _telemetryBuffer;

    public GetRecentCapturedSpansUseCase(ITelemetryBuffer telemetryBuffer)
    {
        _telemetryBuffer = telemetryBuffer;
    }

    public Task<Result<IReadOnlyList<CapturedSpanDto>>> ExecuteAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        var spans = _telemetryBuffer.GetRecentSpans(count);
        return Task.FromResult(Result.Success(spans));
    }
}
