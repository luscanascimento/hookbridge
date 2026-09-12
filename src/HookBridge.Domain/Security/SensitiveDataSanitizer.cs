using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HookBridge.Domain.Security;

/// <summary>
/// High-performance, zero-leakage sanitizer for PII, secrets, credentials, and sensitive HTTP headers.
/// Ensures that logs, audit entries, attempt payloads, and traces never leak sensitive information.
/// </summary>
public static partial class SensitiveDataSanitizer
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly HashSet<string> SensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Proxy-Authorization",
        "X-Api-Key",
        "ApiKey",
        "Cookie",
        "Set-Cookie",
        "X-HookBridge-Secret",
        "X-HookBridge-Signature",
        "X-Auth-Token",
        "X-Secret-Key",
        "Secret-Key",
        "Private-Key"
    };

    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "adminPassword",
        "initialPassword",
        "oldPassword",
        "newPassword",
        "secret",
        "secretKey",
        "clientSecret",
        "plainSecret",
        "token",
        "accessToken",
        "refreshToken",
        "apiKey",
        "plainKey",
        "keyHash",
        "secretHash",
        "privateKey",
        "authorization",
        "credential",
        "creditCard",
        "cardNumber",
        "cvv",
        "ssn"
    };

    private static readonly Regex EmbeddedCredentialsRegex = new(
        @"://([^:/?#\s]+):([^@/?#\s]+)@",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex SensitiveQueryParamRegex = new(
        @"(?<=[?&](?:token|key|api_key|apiKey|secret|password|sig|signature)=)([^&#\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex BearerTokenRegex = new(
        @"^(Bearer\s+)[^\s]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex BasicAuthRegex = new(
        @"^(Basic\s+)[^\s]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Checks whether a given header name is considered sensitive.
    /// </summary>
    public static bool IsSensitiveHeader(string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            return false;
        }

        return SensitiveHeaderNames.Contains(headerName.Trim())
            || headerName.Contains("token", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("password", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("auth", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("apikey", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks whether a given property name is considered sensitive.
    /// </summary>
    public static bool IsSensitiveProperty(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return SensitivePropertyNames.Contains(propertyName.Trim())
            || propertyName.Contains("password", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("token", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("credential", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sanitizes an HTTP header value based on header name.
    /// </summary>
    public static string SanitizeHeader(string headerName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!IsSensitiveHeader(headerName))
        {
            return value;
        }

        if (BearerTokenRegex.IsMatch(value))
        {
            return $"Bearer {RedactedValue}";
        }

        if (BasicAuthRegex.IsMatch(value))
        {
            return $"Basic {RedactedValue}";
        }

        if (value.StartsWith("hb_live_", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("hb_test_", StringComparison.OrdinalIgnoreCase))
        {
            return MaskApiKey(value);
        }

        return RedactedValue;
    }

    /// <summary>
    /// Sanitizes a dictionary of HTTP headers.
    /// </summary>
    public static Dictionary<string, string> SanitizeHeaders(IEnumerable<KeyValuePair<string, string>> headers)
    {
        var sanitized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in headers)
        {
            sanitized[key] = SanitizeHeader(key, value);
        }
        return sanitized;
    }

    /// <summary>
    /// Sanitizes a JSON representation of HTTP headers (e.g. stored in webhook attempts).
    /// </summary>
    public static string SanitizeHeadersJson(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return "{}";
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
            if (dict != null)
            {
                var sanitized = SanitizeHeaders(dict);
                return JsonSerializer.Serialize(sanitized);
            }
        }
        catch
        {
            // Fallback to recursive JSON sanitization if not a simple string dictionary
            return SanitizeJson(headersJson);
        }

        return "{}";
    }

    /// <summary>
    /// Recursively traverses a JSON payload and replaces sensitive property values with [REDACTED].
    /// </summary>
    public static string SanitizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null)
            {
                return json;
            }

            SanitizeJsonNode(node);
            return node.ToJsonString();
        }
        catch
        {
            // If malformed JSON, return safe redacted notice
            return "{\"sanitization\":\"[Malformed JSON - Suppressed]\"}";
        }
    }

    private static void SanitizeJsonNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var keysToRedact = new List<string>();
            foreach (var property in obj)
            {
                if (property.Value is JsonValue && IsSensitiveProperty(property.Key))
                {
                    keysToRedact.Add(property.Key);
                }
                else if (property.Value != null)
                {
                    SanitizeJsonNode(property.Value);
                }
            }

            foreach (var key in keysToRedact)
            {
                obj[key] = RedactedValue;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                {
                    SanitizeJsonNode(item);
                }
            }
        }
    }

    /// <summary>
    /// Sanitizes embedded basic auth credentials and sensitive query parameters from URLs.
    /// </summary>
    public static string SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        // 1. Redact embedded credentials: https://user:pass@host -> https://user:[REDACTED]@host
        var sanitized = EmbeddedCredentialsRegex.Replace(url, "://$1:" + RedactedValue + "@");

        // 2. Redact sensitive query parameters: ?token=xyz -> ?token=[REDACTED]
        sanitized = SensitiveQueryParamRegex.Replace(sanitized, RedactedValue);

        return sanitized;
    }

    /// <summary>
    /// Masks an API key preserving only prefix and last 4 characters.
    /// </summary>
    public static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return RedactedValue;
        }

        var trimmed = apiKey.Trim();
        if (trimmed.Length <= 12)
        {
            return RedactedValue;
        }

        var prefix = trimmed[..8]; // e.g. "hb_live_" or "hb_test_"
        var suffix = trimmed[^4..];
        return $"{prefix}...{suffix}";
    }

    /// <summary>
    /// Masks an email address for privacy (e.g. john.doe@example.com -> j***e@example.com).
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 1)
        {
            return "***" + trimmed[Math.Max(0, atIndex)..];
        }

        var local = trimmed[..atIndex];
        var domain = trimmed[atIndex..];

        var maskedLocal = local.Length switch
        {
            2 => $"{local[0]}*{local[1]}",
            _ => $"{local[0]}***{local[^1]}"
        };

        return $"{maskedLocal}{domain}";
    }
}
