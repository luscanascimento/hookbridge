using System.Net;
using System.Text.RegularExpressions;

namespace HookBridge.Application.Common.Security;

/// <summary>
/// Defensive input sanitization and XSS / injection pattern detection utility.
/// </summary>
public static partial class InputSanitizer
{
    private static readonly Regex XssPatternRegex = new(
        @"(?:<script\b[^>]*>[\s\S]*?<\/script>|<script\b[^>]*\/?>|javascript\s*:[^\s]*|vbscript\s*:[^\s]*|data\s*:\s*text\/html[^\s]*|on[a-z]+\s*=\s*(?:['""][^'""]*['""]|[^\s>]+)|<iframe\b|<object\b|<embed\b|<applet\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private static readonly Regex SlugRegex = new(
        @"^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Checks whether the string contains potential Cross-Site Scripting (XSS) vectors or script payloads.
    /// </summary>
    public static bool ContainsXssVectors(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var decoded = WebUtility.HtmlDecode(input);
        return XssPatternRegex.IsMatch(input) || XssPatternRegex.IsMatch(decoded);
    }

    /// <summary>
    /// HTML-encodes user input to prevent stored/reflected XSS rendering.
    /// </summary>
    public static string SanitizeText(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return WebUtility.HtmlEncode(input.Trim());
    }

    /// <summary>
    /// Validates that a slug strictly matches URL-safe kebab-case format, preventing directory traversal and injection.
    /// </summary>
    public static bool IsValidSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 64)
        {
            return false;
        }

        return SlugRegex.IsMatch(slug.ToLowerInvariant());
    }
}
