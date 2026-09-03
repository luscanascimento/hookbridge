namespace HookBridge.Application.Common;

public static class SubscriptionPatternMatcher
{
    /// <summary>
    /// Evaluates if an incoming event type matches a subscription pattern (supports exact match, '*', or prefix wildcards like 'order.*').
    /// </summary>
    public static bool Matches(string pattern, string eventType)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(eventType))
        {
            return false;
        }

        var normalizedPattern = pattern.Trim().ToLowerInvariant();
        var normalizedEvent = eventType.Trim().ToLowerInvariant();

        // 1. Universal Wildcard
        if (normalizedPattern == "*")
        {
            return true;
        }

        // 2. Exact Match
        if (string.Equals(normalizedPattern, normalizedEvent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 3. Hierarchical Prefix Wildcard (e.g. "order.*" matches "order.created", "order.payment.success")
        if (normalizedPattern.EndsWith(".*", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = normalizedPattern[..^2];
            return normalizedEvent.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalizedEvent, prefix, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
