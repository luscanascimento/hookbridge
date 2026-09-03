using HookBridge.Domain.Common;

namespace HookBridge.Application.Abstractions;

/// <summary>
/// Validates outbound target URLs against Server-Side Request Forgery (SSRF) vulnerabilities,
/// ensuring URLs do not target loopback, private RFC 1918 networks, link-local, or cloud metadata services.
/// </summary>
public interface ISsrfGuard
{
    /// <summary>
    /// Validates the provided URL against SSRF rules and resolved IP addresses.
    /// </summary>
    Task<Result<bool>> ValidateUrlAsync(string url, CancellationToken cancellationToken = default);
}
