using System.Net;
using System.Net.Sockets;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.Infrastructure.Security;

public sealed partial class SsrfGuard : ISsrfGuard
{
    private readonly SsrfOptions _options;
    private readonly ILogger<SsrfGuard> _logger;

    public SsrfGuard(IOptions<SsrfOptions> options, ILogger<SsrfGuard> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "SSRF block: Destination host '{Host}' matches prohibited hostname patterns.")]
    private static partial void LogProhibitedHost(ILogger logger, string host);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "SSRF block: Destination IP '{IpAddress}' is private, loopback, or cloud metadata.")]
    private static partial void LogProhibitedIp(ILogger logger, IPAddress ipAddress);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "SSRF block: Host '{Host}' resolved to prohibited IP '{IpAddress}'.")]
    private static partial void LogProhibitedResolvedIp(ILogger logger, string host, IPAddress ipAddress);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "DNS resolution failed for destination host '{Host}'.")]
    private static partial void LogDnsResolutionFailed(ILogger logger, Exception ex, string host);

    public async Task<Result<bool>> ValidateUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Result.Failure<bool>(DomainError.Validation("Ssrf.EmptyUrl", "The destination URL cannot be empty."));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Result.Failure<bool>(DomainError.Validation("Ssrf.InvalidUri", "The destination URL is not a valid absolute URI."));
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return Result.Failure<bool>(DomainError.Validation("Ssrf.InvalidScheme", $"The URI scheme '{uri.Scheme}' is not allowed. Only HTTP and HTTPS are permitted."));
        }

        if (uri.Port != 80 && uri.Port != 443 && (uri.Port < 1024 || uri.Port > 65535))
        {
            return Result.Failure<bool>(DomainError.Validation("Ssrf.InvalidPort", $"Port {uri.Port} is prohibited for webhook deliveries."));
        }

        if (!_options.Enabled)
        {
            return Result.Success(true);
        }

        var host = uri.Host.Trim().ToLowerInvariant();

        // 1. Check explicitly allowed hosts
        if (_options.AllowedHosts.Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Success(true);
        }

        // 2. Block direct localhost / reserved hostnames
        if (IsProhibitedHostname(host))
        {
            LogProhibitedHost(_logger, host);
            return Result.Failure<bool>(DomainError.Validation("Ssrf.ProhibitedHost", $"Destination host '{host}' is prohibited for webhook endpoints."));
        }

        // 3. Direct IP checking
        if (IPAddress.TryParse(host, out var directIp))
        {
            if (IsProhibitedIpAddress(directIp))
            {
                LogProhibitedIp(_logger, directIp);
                return Result.Failure<bool>(DomainError.Validation("Ssrf.ProhibitedIp", $"Destination IP '{directIp}' is prohibited."));
            }

            return Result.Success(true);
        }

        // 4. DNS Resolution IP checking (if enabled)
        if (_options.ResolveDns)
        {
            try
            {
                var resolvedAddresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
                if (resolvedAddresses.Length == 0)
                {
                    return Result.Failure<bool>(DomainError.Validation("Ssrf.DnsResolutionFailed", $"Could not resolve destination host '{host}' via DNS."));
                }

                foreach (var ip in resolvedAddresses)
                {
                    if (IsProhibitedIpAddress(ip))
                    {
                        LogProhibitedResolvedIp(_logger, host, ip);
                        return Result.Failure<bool>(DomainError.Validation("Ssrf.ProhibitedResolvedIp", $"Destination host '{host}' resolves to prohibited internal address '{ip}'."));
                    }
                }
            }
            catch (SocketException ex)
            {
                LogDnsResolutionFailed(_logger, ex, host);
                return Result.Failure<bool>(DomainError.Validation("Ssrf.DnsResolutionFailed", $"DNS resolution failed for host '{host}': {ex.Message}"));
            }
        }

        return Result.Success(true);
    }

    private static bool IsProhibitedHostname(string host)
    {
        return host == "localhost"
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || host == "metadata.google.internal"
            || host == "instance-data"
            || host == "169.254.169.254";
    }

    public static bool IsProhibitedIpAddress(IPAddress ipAddress)
    {
        if (IPAddress.IsLoopback(ipAddress))
        {
            return true;
        }

        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6SiteLocal || ipAddress.IsIPv6Multicast)
            {
                return true;
            }

            if (ipAddress.IsIPv4MappedToIPv6)
            {
                return IsProhibitedIpAddress(ipAddress.MapToIPv4());
            }

            var v6Bytes = ipAddress.GetAddressBytes();
            // fc00::/7 Unique Local Address
            if ((v6Bytes[0] & 0xfe) == 0xfc)
            {
                return true;
            }

            return false;
        }

        var bytes = ipAddress.GetAddressBytes();

        // 0.0.0.0/8 (Current network)
        if (bytes[0] == 0) return true;

        // 10.0.0.0/8 (Private RFC 1918)
        if (bytes[0] == 10) return true;

        // 100.64.0.0/10 (Carrier-Grade NAT)
        if (bytes[0] == 100 && (bytes[1] & 0xc0) == 64) return true;

        // 127.0.0.0/8 (Loopback)
        if (bytes[0] == 127) return true;

        // 169.254.0.0/16 (Link-Local & Cloud Metadata 169.254.169.254)
        if (bytes[0] == 169 && bytes[1] == 254) return true;

        // 172.16.0.0/12 (Private RFC 1918)
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;

        // 192.0.2.0/24 (TEST-NET-1)
        if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) return true;

        // 192.168.0.0/16 (Private RFC 1918)
        if (bytes[0] == 192 && bytes[1] == 168) return true;

        // 198.51.100.0/24 (TEST-NET-2)
        if (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) return true;

        // 203.0.113.0/24 (TEST-NET-3)
        if (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) return true;

        // 224.0.0.0/4 (Multicast)
        if (bytes[0] >= 224 && bytes[0] <= 239) return true;

        // 240.0.0.0/4 (Reserved / Broadcast)
        if (bytes[0] >= 240) return true;

        return false;
    }
}
