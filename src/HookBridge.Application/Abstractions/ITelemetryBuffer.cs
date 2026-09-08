using HookBridge.Application.ControlPlane.UseCases.Observability;

namespace HookBridge.Application.Abstractions;

public interface ITelemetryBuffer
{
    IReadOnlyList<CapturedSpanDto> GetRecentSpans(int count = 50);
    int TotalRecordedCount { get; }
    void Clear();
}
