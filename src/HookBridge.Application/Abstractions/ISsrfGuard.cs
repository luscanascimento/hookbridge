using HookBridge.Domain.Common;

namespace HookBridge.Application.Abstractions;

/// <summary>
/// Validates outbound target URLs against Server-Side Request Forgery (SSRF) vulnerabilities,
/// ensuring URLs do not target loopback, private RFC 1918 networks, link-local, cloud metadata services,
/// or evasion attempts (e.g. integer IPs, hex/octal IPs, open redirects, and DNS rebinding).
/// </summary>
public interface ISsrfGuard
{
    /// <summary>
    /// Validates the provided URL against SSRF rules, IP representations, and resolved IP addresses.
    /// </summary>
    Task<Result<bool>> ValidateUrlAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a potential HTTP redirect URL from an initial source URL to prevent Open Redirect SSRF attacks.
    /// </summary>
    Task<Result<bool>> ValidateRedirectUrlAsync(string currentUrl, string redirectUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a hardened SocketsHttpHandler that enforces IP address verification at socket connection time
    /// to eliminate Time-of-Check to Time-of-Use (TOCTOU) DNS Rebinding attacks.
    /// </summary>
    SocketsHttpHandler CreateSafeSocketsHttpHandler();
}
