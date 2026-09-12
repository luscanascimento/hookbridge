using System.Globalization;
using System.Net;
using System.Net.Sockets;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.Infrastructure.Security;

/// <summary>
/// Hardened SSRF defense engine defending against private IPs, loopbacks, cloud metadata (IMDS),
/// decimal/octal/hex IP evasions, open redirects, and DNS rebinding (TOCTOU) at the socket level.
/// </summary>
public sealed partial class SsrfGuard : ISsrfGuard
{
    private static readonly HashSet<int> ProhibitedPorts = new()
    {
        21,    // FTP
        22,    // SSH
        23,    // Telnet
        25,    // SMTP
        53,    // DNS
        69,    // TFTP
        110,   // POP3
        143,   // IMAP
        389,   // LDAP
        636,   // LDAPS
        1433,  // MS SQL
        1521,  // Oracle DB
        2375,  // Docker daemon unencrypted
        2376,  // Docker daemon TLS
        3306,  // MySQL
        5432,  // PostgreSQL
        6379,  // Redis
        6443,  // Kubernetes API
        9200,  // Elasticsearch
        10250, // Kubelet
        11211, // Memcached
        27017  // MongoDB
    };

    private static readonly string[] ProhibitedDomainSuffixes =
    [
        ".localhost",
        ".local",
        ".internal",
        ".lan",
        ".home",
        ".corp",
        ".cluster.local",
        ".svc",
        ".localdomain"
    ];

    private static readonly string[] ProhibitedExactHostnames =
    [
        "localhost",
        "metadata.google.internal",
        "instance-data",
        "host.docker.internal",
        "gateway.docker.internal",
        "kubernetes.default.svc",
        "169.254.169.254",
        "169.254.170.2",
        "100.100.100.200",
        "192.0.0.192"
    ];

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

        // Prohibit embedded credentials in userinfo (e.g. http://user:pass@host)
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return Result.Failure<bool>(DomainError.Validation("Ssrf.UserInfoProhibited", "Embedded userinfo/credentials in destination URLs are prohibited."));
        }

        // Prohibit dangerous infrastructure/database ports
        if (ProhibitedPorts.Contains(uri.Port))
        {
            return Result.Failure<bool>(DomainError.Validation("Ssrf.InvalidPort", $"Port {uri.Port} is prohibited for webhook deliveries."));
        }

        if (uri.Port != 80 && uri.Port != 443 && (uri.Port < 1024 || uri.Port > 65535))
        {
            return Result.Failure<bool>(DomainError.Validation("Ssrf.InvalidPort", $"Port {uri.Port} is outside the permitted ranges for webhook endpoints."));
        }

        if (!_options.Enabled)
        {
            return Result.Success(true);
        }

        var host = uri.DnsSafeHost.Trim().ToLowerInvariant();

        // 1. Check explicitly allowed hosts (whitelist bypass for testing)
        if (_options.AllowedHosts.Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Success(true);
        }

        // 2. Block direct localhost / cloud metadata / container hostnames
        if (IsProhibitedHostname(host))
        {
            LogProhibitedHost(_logger, host);
            return Result.Failure<bool>(DomainError.Validation("Ssrf.ProhibitedHost", $"Destination host '{host}' is prohibited for webhook endpoints."));
        }

        // 3. Alternative IP representation evasion detection (e.g. 2130706433, 0x7f000001, 0177.0.0.1)
        if (TryNormalizeAlternativeIp(host, out var alternativeIp) && alternativeIp != null)
        {
            if (IsProhibitedIpAddress(alternativeIp))
            {
                LogProhibitedIp(_logger, alternativeIp);
                return Result.Failure<bool>(DomainError.Validation("Ssrf.ProhibitedIp", $"Destination host '{host}' resolves to prohibited internal address '{alternativeIp}'."));
            }
            return Result.Success(true);
        }

        // 4. Standard Direct IP checking (IPv4 and IPv6)
        if (IPAddress.TryParse(host, out var directIp))
        {
            if (IsProhibitedIpAddress(directIp))
            {
                LogProhibitedIp(_logger, directIp);
                return Result.Failure<bool>(DomainError.Validation("Ssrf.ProhibitedIp", $"Destination IP '{directIp}' is private, loopback, or metadata address."));
            }

            return Result.Success(true);
        }

        // 5. DNS Resolution IP checking (if enabled)
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

    public async Task<Result<bool>> ValidateRedirectUrlAsync(string currentUrl, string redirectUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(redirectUrl))
        {
            return Result.Failure<bool>(DomainError.Validation("Ssrf.EmptyRedirectUrl", "Redirect destination URL cannot be empty."));
        }

        Uri targetUri;
        if (Uri.TryCreate(redirectUrl, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            targetUri = absoluteUri;
        }
        else if (Uri.TryCreate(currentUrl, UriKind.Absolute, out var baseUri) &&
                 Uri.TryCreate(baseUri, redirectUrl, out var combinedUri))
        {
            targetUri = combinedUri;
        }
        else
        {
            return Result.Failure<bool>(DomainError.Validation("Ssrf.InvalidRedirectUri", "Redirect URI is malformed and could not be resolved."));
        }

        return await ValidateUrlAsync(targetUri.ToString(), cancellationToken);
    }

    public SocketsHttpHandler CreateSafeSocketsHttpHandler()
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (context, cancellationToken) =>
            {
                var host = context.DnsEndPoint.Host;
                var port = context.DnsEndPoint.Port;

                if (_options.Enabled && !_options.AllowedHosts.Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase)))
                {
                    if (IsProhibitedHostname(host))
                    {
                        LogProhibitedHost(_logger, host);
                        throw new SocketException((int)SocketError.AccessDenied);
                    }

                    if (TryNormalizeAlternativeIp(host, out var altIp) && altIp != null && IsProhibitedIpAddress(altIp))
                    {
                        LogProhibitedIp(_logger, altIp);
                        throw new SocketException((int)SocketError.AccessDenied);
                    }

                    var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
                    if (addresses.Length == 0)
                    {
                        throw new SocketException((int)SocketError.HostNotFound);
                    }

                    var targetIp = addresses.FirstOrDefault(ip => !IsProhibitedIpAddress(ip));
                    if (targetIp == null)
                    {
                        LogProhibitedResolvedIp(_logger, host, addresses[0]);
                        throw new SocketException((int)SocketError.AccessDenied);
                    }

                    var socket = new Socket(targetIp.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };

                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(targetIp, port), cancellationToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
                else
                {
                    var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
                    if (addresses.Length == 0)
                    {
                        throw new SocketException((int)SocketError.HostNotFound);
                    }

                    var socket = new Socket(addresses[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };

                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(addresses[0], port), cancellationToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            }
        };
    }

    /// <summary>
    /// Detects and normalizes alternative IP representations used in SSRF bypass attempts:
    /// pure decimal integers (e.g. 2130706433), hex integers (e.g. 0x7f000001), and dotted octal/hex notation (e.g. 0177.0.0.1).
    /// </summary>
    public static bool TryNormalizeAlternativeIp(string host, out IPAddress? normalizedIp)
    {
        normalizedIp = null;
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        // 1. Pure decimal integer (e.g., 2130706433 -> 127.0.0.1)
        if (uint.TryParse(host, NumberStyles.None, CultureInfo.InvariantCulture, out var decimalIp))
        {
            normalizedIp = ConvertUintToIPv4(decimalIp);
            return true;
        }

        // 2. Pure hexadecimal integer (e.g., 0x7f000001 -> 127.0.0.1)
        if (host.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var hexSpan = host.AsSpan(2);
            if (uint.TryParse(hexSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexIp))
            {
                normalizedIp = ConvertUintToIPv4(hexIp);
                return true;
            }
        }

        // 3. Dotted octal/hex notation (e.g., 0177.0.0.1 or 0x7f.0.0.1)
        if (host.Contains('.'))
        {
            var parts = host.Split('.');
            if (parts.Length == 4)
            {
                var bytes = new byte[4];
                var hasAlternativeFormat = false;

                for (var i = 0; i < 4; i++)
                {
                    var part = parts[i];
                    if (string.IsNullOrEmpty(part))
                    {
                        return false;
                    }

                    if (part.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAlternativeFormat = true;
                        if (!byte.TryParse(part.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
                        {
                            return false;
                        }
                    }
                    else if (part.Length > 1 && part.StartsWith('0'))
                    {
                        hasAlternativeFormat = true;
                        try
                        {
                            var octalVal = Convert.ToByte(part, 8);
                            bytes[i] = octalVal;
                        }
                        catch
                        {
                            return false;
                        }
                    }
                    else if (byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var b))
                    {
                        bytes[i] = b;
                    }
                    else
                    {
                        return false;
                    }
                }

                if (hasAlternativeFormat)
                {
                    normalizedIp = new IPAddress(bytes);
                    return true;
                }
            }
        }

        return false;
    }

    private static IPAddress ConvertUintToIPv4(uint value)
    {
        var bytes = new byte[4]
        {
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF)
        };
        return new IPAddress(bytes);
    }

    private static bool IsProhibitedHostname(string host)
    {
        if (ProhibitedExactHostnames.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return ProhibitedDomainSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsProhibitedIpAddress(IPAddress ipAddress)
    {
        if (IPAddress.IsLoopback(ipAddress))
        {
            return true;
        }

        if (ipAddress.Equals(IPAddress.Any) || ipAddress.Equals(IPAddress.IPv6Any) || ipAddress.Equals(IPAddress.None))
        {
            return true;
        }

        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6SiteLocal || ipAddress.IsIPv6Multicast || ipAddress.IsIPv6Teredo)
            {
                return true;
            }

            if (ipAddress.IsIPv4MappedToIPv6)
            {
                return IsProhibitedIpAddress(ipAddress.MapToIPv4());
            }

            var v6Bytes = ipAddress.GetAddressBytes();

            // ::1 (Loopback)
            if (ipAddress.Equals(IPAddress.IPv6Loopback))
            {
                return true;
            }

            // fc00::/7 Unique Local Address (Private IPv6)
            if ((v6Bytes[0] & 0xfe) == 0xfc)
            {
                return true;
            }

            // AWS IMDSv2 IPv6 (fd00:ec2::254)
            if (v6Bytes[0] == 0xfd && v6Bytes[1] == 0x00 && v6Bytes[2] == 0x0e && v6Bytes[3] == 0xc2)
            {
                return true;
            }

            return false;
        }

        var bytes = ipAddress.GetAddressBytes();

        // 0.0.0.0/8 (Current network / default route)
        if (bytes[0] == 0) return true;

        // 10.0.0.0/8 (Private RFC 1918)
        if (bytes[0] == 10) return true;

        // 100.64.0.0/10 (Carrier-Grade NAT & Alibaba IMDS 100.100.100.200)
        if (bytes[0] == 100 && (bytes[1] & 0xc0) == 64) return true;
        if (bytes[0] == 100 && bytes[1] == 100 && bytes[2] == 100 && bytes[3] == 200) return true;

        // 127.0.0.0/8 (Loopback)
        if (bytes[0] == 127) return true;

        // 169.254.0.0/16 (Link-Local & Cloud Metadata 169.254.169.254, ECS 169.254.170.2)
        if (bytes[0] == 169 && bytes[1] == 254) return true;

        // 172.16.0.0/12 (Private RFC 1918)
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;

        // 192.0.0.0/24 (IETF Protocol Assignments & Oracle IMDS 192.0.0.192)
        if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) return true;

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
