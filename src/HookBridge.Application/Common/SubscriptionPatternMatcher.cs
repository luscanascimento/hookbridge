namespace HookBridge.Application.Common;

public static class SubscriptionPatternMatcher
{
    /// <summary>
    /// Evaluates if an incoming event type matches a subscription pattern (supports exact match, '*', or prefix wildcards like 'order.*').
    /// Optimized for zero heap allocations using ReadOnlySpan and case-insensitive ordinal comparisons.
    /// </summary>
    public static bool Matches(string pattern, string eventType)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(eventType))
        {
            return false;
        }

        var patternSpan = pattern.AsSpan().Trim();
        var eventSpan = eventType.AsSpan().Trim();

        if (patternSpan.IsEmpty || eventSpan.IsEmpty)
        {
            return false;
        }

        // 1. Universal Wildcard
        if (patternSpan.SequenceEqual("*"))
        {
            return true;
        }

        // 2. Exact Match
        if (patternSpan.Equals(eventSpan, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 3. Hierarchical Prefix Wildcard (e.g. "order.*" matches "order.created", "order.payment.success", or "order")
        if (patternSpan.EndsWith(".*", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = patternSpan[..^2];
            if (eventSpan.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (eventSpan.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                eventSpan.Length > prefix.Length &&
                eventSpan[prefix.Length] == '.')
            {
                return true;
            }
        }

        return false;
    }
}

