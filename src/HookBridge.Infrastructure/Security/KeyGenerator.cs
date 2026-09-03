using System.Security.Cryptography;
using System.Text;
using HookBridge.Application.Abstractions;

namespace HookBridge.Infrastructure.Security;

public sealed class KeyGenerator : IApiKeyGenerator
{
    public (string PlainKey, string KeyPrefix, string KeyHash) GenerateApiKey(string environment = "live")
    {
        var env = string.Equals(environment, "test", StringComparison.OrdinalIgnoreCase) ? "test" : "live";
        var randomBytes = RandomNumberGenerator.GetBytes(24);
        var randomHex = Convert.ToHexString(randomBytes).ToLowerInvariant();

        // Standard API Key format: hb_live_... or hb_test_...
        var plainKey = $"hb_{env}_{randomHex}";
        var prefix = plainKey.Length >= 16 ? plainKey[..16] : plainKey;
        var hash = ComputeHash(plainKey);

        return (plainKey, prefix, hash);
    }

    public (string PlainSecret, string SecretPrefix, string SecretHash) GenerateWebhookSecret()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var randomHex = Convert.ToHexString(randomBytes).ToLowerInvariant();

        // Standard Webhook Secret format: whsec_...
        var plainSecret = $"whsec_{randomHex}";
        var prefix = plainSecret.Length >= 14 ? plainSecret[..14] : plainSecret;
        var hash = ComputeHash(plainSecret);

        return (plainSecret, prefix, hash);
    }

    public string ComputeHash(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
