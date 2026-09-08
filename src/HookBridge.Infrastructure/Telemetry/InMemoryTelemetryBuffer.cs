using System.Collections.Concurrent;
using System.Diagnostics;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.UseCases.Observability;
using OpenTelemetry;

namespace HookBridge.Infrastructure.Telemetry;

public sealed class InMemoryTelemetryBuffer : BaseProcessor<Activity>, ITelemetryBuffer
{
    private readonly ConcurrentQueue<CapturedSpanDto> _recentSpans = new();
    private const int MaxBufferSize = 200;

    public override void OnEnd(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var snapshot = new CapturedSpanDto(
            TraceId: activity.TraceId.ToString(),
            SpanId: activity.SpanId.ToString(),
            ParentSpanId: activity.ParentSpanId != default ? activity.ParentSpanId.ToString() : null,
            OperationName: activity.OperationName,
            SourceName: activity.Source.Name,
            DurationMs: activity.Duration.TotalMilliseconds,
            StartTime: activity.StartTimeUtc,
            Status: activity.Status.ToString(),
            Tags: activity.TagObjects.ToDictionary(
                kv => kv.Key,
                kv => kv.Value?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase),
            Baggage: activity.Baggage.ToDictionary(
                kv => kv.Key,
                kv => kv.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
        );

        _recentSpans.Enqueue(snapshot);

        while (_recentSpans.Count > MaxBufferSize && _recentSpans.TryDequeue(out _))
        {
            // Keep buffer within MaxBufferSize limit
        }
    }

    public IReadOnlyList<CapturedSpanDto> GetRecentSpans(int count = 50)
    {
        return _recentSpans
            .TakeLast(Math.Clamp(count, 1, MaxBufferSize))
            .Reverse()
            .ToList();
    }

    public int TotalRecordedCount => _recentSpans.Count;

    public void Clear()
    {
        _recentSpans.Clear();
    }
}
