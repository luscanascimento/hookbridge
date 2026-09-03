namespace HookBridge.Infrastructure.Security;

public sealed class SsrfOptions
{
    public const string SectionName = "Ssrf";

    /// <summary>
    /// Whether SSRF protections are enabled. Enabled by default in all environments.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to resolve hostnames via DNS to check underlying IP addresses.
    /// Default true. Can be disabled in unit tests/mock environments.
    /// </summary>
    public bool ResolveDns { get; set; } = true;

    /// <summary>
    /// List of explicitly allowed hostnames/domains (e.g. for mock servers or internal test webhooks).
    /// </summary>
    public List<string> AllowedHosts { get; set; } = new();
}
